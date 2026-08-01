using Clinic.Api.DTOs.Identity;
using Clinic.Domain.Entites.Identity;
using Clinic.Api.Extensions;
using Clinic.Domain.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Clinic.Api.Controllers
{
    public class AccountsController : APIBaseController
    {
        /// <summary>
        /// A throwaway hash of a throwaway password, used to equalise the cost of a login attempt
        /// against an address that does not exist. See Login.
        /// </summary>
        private static readonly string DecoyPasswordHash =
            new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString());

        /// <summary>Deliberately identical for "no such account" and "wrong password".</summary>
        private const string InvalidCredentials = "Invalid email or password.";

        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(UserManager<AppUser> userManager ,
            ITokenService tokenService ,
             SignInManager<AppUser> signInManager,
             IPasswordHasher<AppUser> passwordHasher,
             ILogger<AccountsController> logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        

        // Register
        //
        // Accounts in a clinical system are provisioned, not self-served. Anonymous registration let
        // anyone create an account, and - once TODO #11 made roles real - would let them ask for one.
        // Only an administrator may create accounts now.
        //
        // Because the caller is a trusted administrator, this endpoint CAN be specific about
        // failures ("that address is already registered"). The same message on the anonymous Login
        // endpoint would be an account-enumeration oracle; here it is simply useful.
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpPost("Register")]
        [ProducesResponseType(typeof(RegisteredUserDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RegisteredUserDto>> Register(RegisterDto model)
        {
            var role = string.IsNullOrWhiteSpace(model.Role) ? ClinicRoles.Patient : model.Role.Trim();

            if (!ClinicRoles.All.Contains(role, StringComparer.Ordinal))
            {
                ModelState.AddModelError(nameof(model.Role),
                    $"Unknown role '{role}'. Valid roles are: {string.Join(", ", ClinicRoles.All)}.");
                return ValidationProblem(ModelState);
            }

            if (await _userManager.FindByEmailAsync(model.Email) is not null)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "An account with that email address already exists."
                });
            }

            var user = new AppUser()
            {
                DisplayName = model.DisplayName,
                Email = model.Email,

                // The email IS the username. Deriving it from the local part meant
                // alice@example.com and alice@other.com both became "alice", so the second
                // registration failed with an opaque duplicate-username error.
                UserName = model.Email,
                PhoneNumber = model.PhoneNumber,

                // An administrator vouched for this address. A real confirmation round trip needs
                // an email sender, which this application does not have yet.
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user , model.Password);

            if (!result.Succeeded)
            {
                // Returning result.Errors raw leaked Identity's internal error codes in a shape
                // nothing else in the API uses. Fold them into the standard validation contract.
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return ValidationProblem(ModelState);
            }

            var assigned = await _userManager.AddToRoleAsync(user, role);

            if (!assigned.Succeeded)
            {
                // An account with no role can authenticate but do nothing, and the reason is
                // invisible. Do not leave one behind.
                await _userManager.DeleteAsync(user);

                throw new InvalidOperationException(
                    $"Failed to assign role '{role}' to the new account: " +
                    string.Join("; ", assigned.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("Account {UserId} provisioned with role {Role} by {Administrator}.",
                user.Id, role, User.FindFirstValue(ClaimTypes.NameIdentifier));

            // No token. See RegisteredUserDto - issuing one here would hand the administrator a
            // bearer token impersonating the account they just created.
            return StatusCode(StatusCodes.Status201Created, new RegisteredUserDto
            {
                DisplayName = user.DisplayName,
                Email = user.Email,
                Role = role
            });
        }

        //login
        // Necessarily anonymous: this is where a caller obtains the token everything else requires.
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingServicesExtensions.AuthPolicy)]
        [HttpPost("Login")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
        public async Task<ActionResult<UserDto>> Login(LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user is null)
            {
                // Verifying a password hash dominates the cost of this request. Returning early
                // without doing it made a non-existent address answer measurably faster than a real
                // one, which is a free account-enumeration oracle. Burn the same work on a decoy.
                _passwordHasher.VerifyHashedPassword(new AppUser(), DecoyPasswordHash, model.Password);

                _logger.LogWarning("Failed login for an unknown address from {RemoteIpAddress}.",
                    HttpContext.Connection.RemoteIpAddress);

                return Unauthorized(new ProblemDetails { Title = InvalidCredentials });
            }

            // lockoutOnFailure: true is the whole point. With false, Identity never counted a failed
            // attempt, never incremented AccessFailedCount and never locked anything - the lockout
            // configuration existed but could not fire, leaving passwords open to unlimited guessing.
            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Login attempted against locked-out account {UserId} from {RemoteIpAddress}.",
                    user.Id, HttpContext.Connection.RemoteIpAddress);

                return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
                {
                    Status = StatusCodes.Status423Locked,
                    Title = "This account is temporarily locked after too many failed attempts. Try again later."
                });
            }

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed login for {UserId} from {RemoteIpAddress}.",
                    user.Id, HttpContext.Connection.RemoteIpAddress);

                // Same message and same status as the unknown-address branch: the response must not
                // reveal whether the address is registered.
                return Unauthorized(new ProblemDetails { Title = InvalidCredentials });
            }

            _logger.LogInformation("Successful login for {UserId}.", user.Id);

            return Ok(new UserDto()
            {
                DisplayName = user.DisplayName,
                Email = user.Email,
                Token = await _tokenService.CreateTokenAsync(user, _userManager)
            });
        }
    }
}
