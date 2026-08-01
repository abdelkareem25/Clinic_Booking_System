using Clinic.Api.Controllers;
using Clinic.Api.DTOs.Identity;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Clinic.Tests.Controllers
{
    /// <summary>
    /// Unit tests for TODO #13 (finding H8).
    ///
    /// Login passed lockoutOnFailure: false, so Identity never counted a failed attempt and the
    /// lockout machinery could not fire however it was configured. It also returned early when the
    /// address was unknown, skipping the password hash and answering measurably faster than for a
    /// real account - a free account-enumeration oracle.
    /// </summary>
    public sealed class AccountsControllerLoginTests
    {
        private readonly Mock<UserManager<AppUser>> _userManager;
        private readonly Mock<SignInManager<AppUser>> _signInManager;
        private readonly Mock<IPasswordHasher<AppUser>> _passwordHasher = new();
        private readonly Mock<ITokenService> _tokenService = new();

        private static readonly AppUser KnownUser = new()
        {
            Id = "u1", Email = "staff@clinic.local", UserName = "staff@clinic.local", DisplayName = "Staff"
        };

        public AccountsControllerLoginTests()
        {
            _userManager = new Mock<UserManager<AppUser>>(
                new Mock<IUserStore<AppUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

            _signInManager = new Mock<SignInManager<AppUser>>(
                _userManager.Object,
                new Mock<IHttpContextAccessor>().Object,
                new Mock<IUserClaimsPrincipalFactory<AppUser>>().Object,
                null!, null!, null!, null!);

            _tokenService.Setup(t => t.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<UserManager<AppUser>>()))
                         .ReturnsAsync("a-token");
        }

        private AccountsController CreateSut() =>
            new(_userManager.Object, _tokenService.Object, _signInManager.Object,
                _passwordHasher.Object, NullLogger<AccountsController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

        private void UserExists(AppUser? user) =>
            _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        private void PasswordCheckReturns(SignInResult result) =>
            _signInManager.Setup(m => m.CheckPasswordSignInAsync(
                              It.IsAny<AppUser>(), It.IsAny<string>(), It.IsAny<bool>()))
                          .ReturnsAsync(result);

        private static LoginDto Credentials() =>
            new() { Email = "staff@clinic.local", Password = "whatever" };

        [Fact]
        public async Task Failed_Attempts_Are_Counted_Towards_Lockout()
        {
            // The single line this finding is about.
            UserExists(KnownUser);
            PasswordCheckReturns(SignInResult.Failed);

            await CreateSut().Login(Credentials());

            _signInManager.Verify(m => m.CheckPasswordSignInAsync(
                KnownUser, It.IsAny<string>(), true), Times.Once);

            _signInManager.Verify(m => m.CheckPasswordSignInAsync(
                It.IsAny<AppUser>(), It.IsAny<string>(), false), Times.Never);
        }

        [Fact]
        public async Task A_Locked_Out_Account_Answers_423()
        {
            UserExists(KnownUser);
            PasswordCheckReturns(SignInResult.LockedOut);

            var response = await CreateSut().Login(Credentials());

            var result = Assert.IsType<ObjectResult>(response.Result);
            Assert.Equal(StatusCodes.Status423Locked, result.StatusCode);

            var problem = Assert.IsType<ProblemDetails>(result.Value);
            Assert.Contains("temporarily locked", problem.Title!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task A_Locked_Out_Account_Is_Never_Issued_A_Token()
        {
            UserExists(KnownUser);
            PasswordCheckReturns(SignInResult.LockedOut);

            await CreateSut().Login(Credentials());

            _tokenService.Verify(t => t.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<UserManager<AppUser>>()),
                Times.Never);
        }

        [Fact]
        public async Task An_Unknown_Address_Still_Pays_For_A_Password_Verification()
        {
            // Equalises the response time so the endpoint cannot be used to test whether an address
            // is registered.
            UserExists(null);

            await CreateSut().Login(Credentials());

            _passwordHasher.Verify(h => h.VerifyHashedPassword(
                It.IsAny<AppUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task An_Unknown_Address_And_A_Wrong_Password_Are_Indistinguishable()
        {
            // Same status, same title. Anything else leaks which addresses have accounts.
            UserExists(null);
            var unknown = Assert.IsType<UnauthorizedObjectResult>((await CreateSut().Login(Credentials())).Result);

            UserExists(KnownUser);
            PasswordCheckReturns(SignInResult.Failed);
            var wrongPassword = Assert.IsType<UnauthorizedObjectResult>((await CreateSut().Login(Credentials())).Result);

            Assert.Equal(unknown.StatusCode, wrongPassword.StatusCode);
            Assert.Equal(
                Assert.IsType<ProblemDetails>(unknown.Value).Title,
                Assert.IsType<ProblemDetails>(wrongPassword.Value).Title);
        }

        [Fact]
        public async Task The_Failure_Message_Does_Not_Name_The_Address()
        {
            UserExists(null);

            var response = await CreateSut().Login(Credentials());

            var problem = Assert.IsType<ProblemDetails>(
                Assert.IsType<UnauthorizedObjectResult>(response.Result).Value);

            Assert.DoesNotContain("staff@clinic.local", problem.Title!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task A_Correct_Password_Still_Returns_A_Token()
        {
            // Hardening must not break the working path.
            UserExists(KnownUser);
            PasswordCheckReturns(SignInResult.Success);

            var response = await CreateSut().Login(Credentials());

            var dto = Assert.IsType<UserDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
            Assert.Equal("a-token", dto.Token);
            Assert.Equal("staff@clinic.local", dto.Email);
        }

        [Fact]
        public async Task An_Unknown_Address_Never_Reaches_The_SignIn_Manager()
        {
            UserExists(null);

            await CreateSut().Login(Credentials());

            _signInManager.Verify(m => m.CheckPasswordSignInAsync(
                It.IsAny<AppUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }
    }
}
