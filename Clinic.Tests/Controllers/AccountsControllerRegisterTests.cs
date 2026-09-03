using Clinic.Api.Controllers;
using Clinic.Api.DTOs.Identity;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Service;
using Clinic.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace Clinic.Tests.Controllers
{
    /// <summary>
    /// Unit tests for TODO #14 (finding H9), covering the paths that cannot be reached through the
    /// HTTP surface.
    ///
    /// In particular: the DTO's password regex is stricter than Identity's default policy, so a
    /// password that reaches UserManager.CreateAsync has already satisfied it. The handling of a
    /// failed IdentityResult is therefore only reachable with a mock - but it still has to be right,
    /// because CreateAsync can fail for reasons other than the password.
    /// </summary>
    public sealed class AccountsControllerRegisterTests
    {
        private readonly Mock<UserManager<AppUser>> _userManager;
        private readonly Mock<ITokenService> _tokenService = new();

        public AccountsControllerRegisterTests()
        {
            _userManager = new Mock<UserManager<AppUser>>(
                new Mock<IUserStore<AppUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
            _userManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Success);
        }

        private AccountsController CreateSut()
        {
            var administrator = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "admin-1")], "Test"));

            return new AccountsController(
                _userManager.Object, _tokenService.Object,
                new Mock<SignInManager<AppUser>>(
                    _userManager.Object,
                    new Mock<IHttpContextAccessor>().Object,
                    new Mock<IUserClaimsPrincipalFactory<AppUser>>().Object,
                    null!, null!, null!, null!).Object,
                new Mock<IPasswordHasher<AppUser>>().Object,
                new Mock<IAccountRepository>().Object,
                new StubCurrentTenant(),
                NullLogger<AccountsController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = administrator }
                }
            };
        }

        private static RegisterDto Request(string? role = null) => new()
        {
            DisplayName = "New Person",
            Email = "new@clinic.local",
            Password = "A-Strong-Passw0rd!",
            PhoneNumber = "01000000000",
            Role = role
        };

        [Fact]
        public async Task A_Failed_Create_Does_Not_Leak_Identity_Error_Codes()
        {
            _userManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError
                        {
                            Code = "PasswordRequiresNonAlphanumeric",
                            Description = "Passwords must have at least one non alphanumeric character."
                        }));

            var response = await CreateSut().Register(Request());

            var problem = Assert.IsType<ValidationProblemDetails>(
                Assert.IsType<ObjectResult>(response.Result).Value);

            // The human-readable description is useful; the machine code is Identity's internal
            // vocabulary and was previously returned verbatim.
            var rendered = string.Join(" ", problem.Errors.SelectMany(e => e.Value));
            Assert.Contains("non alphanumeric", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PasswordRequiresNonAlphanumeric", rendered, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_Failed_Create_Does_Not_Attempt_A_Role_Assignment()
        {
            _userManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "nope" }));

            await CreateSut().Register(Request());

            _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task A_Failed_Role_Assignment_Removes_The_Orphaned_Account()
        {
            // An account that exists but holds no role can authenticate and do nothing, and the
            // reason is invisible from the outside. Better to leave nothing behind.
            _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                        .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "role missing" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().Register(Request()));

            _userManager.Verify(m => m.DeleteAsync(It.IsAny<AppUser>()), Times.Once);
        }

        [Fact]
        public async Task The_Created_Account_Uses_The_Full_Email_As_Its_Username()
        {
            AppUser? created = null;
            _userManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                        .Callback<AppUser, string>((u, _) => created = u)
                        .ReturnsAsync(IdentityResult.Success);

            await CreateSut().Register(Request());

            Assert.Equal("new@clinic.local", created!.UserName);
            Assert.Equal("new@clinic.local", created.Email);
            Assert.True(created.EmailConfirmed);
        }

        [Fact]
        public async Task No_Token_Is_Ever_Minted_During_Registration()
        {
            await CreateSut().Register(Request());

            _tokenService.Verify(
                t => t.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<UserManager<AppUser>>()), Times.Never);
        }

        [Fact]
        public async Task An_Unknown_Role_Never_Reaches_The_User_Manager()
        {
            var response = await CreateSut().Register(Request("Superuser"));

            Assert.IsType<ValidationProblemDetails>(Assert.IsType<ObjectResult>(response.Result).Value);
            _userManager.Verify(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task A_Duplicate_Email_Never_Reaches_The_User_Manager()
        {
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
                        .ReturnsAsync(new AppUser { Email = "new@clinic.local" });

            var response = await CreateSut().Register(Request());

            Assert.IsType<ConflictObjectResult>(response.Result);
            _userManager.Verify(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task The_Default_Role_Is_Patient()
        {
            await CreateSut().Register(Request(role: null));

            _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), ClinicRoles.Patient), Times.Once);
        }

        [Fact]
        public async Task Surrounding_Whitespace_In_The_Role_Is_Tolerated()
        {
            await CreateSut().Register(Request($"  {ClinicRoles.Doctor}  "));

            _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), ClinicRoles.Doctor), Times.Once);
        }
    }
}
