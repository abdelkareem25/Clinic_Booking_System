using Clinic.Api.DTOs.Identity;
using Clinic.Api.Helper;
using Clinic.Domain.Entites.Identity;
using Clinic.Api.Extensions;
using Clinic.Domain.Interfaces;
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
        private readonly IAccountRepository _accountRepository;
        private readonly ILogger<AccountsController> _logger;

        /// <summary>
        /// MULTI-TENANT: read ONLY to decide which clinic a newly provisioned account joins.
        ///
        /// This is not tenant filtering leaking into a controller - filtering happens in the
        /// DbContext and no action here does any. AppUser is deliberately not an ITenantEntity, so
        /// it is not stamped automatically, and provisioning is the one decision that genuinely has
        /// to be made here: an account's clinic is a fact about the account, not about the row.
        /// </summary>
        private readonly ICurrentTenant _currentTenant;

        public AccountsController(UserManager<AppUser> userManager ,
            ITokenService tokenService ,
             SignInManager<AppUser> signInManager,
             IPasswordHasher<AppUser> passwordHasher,
             IAccountRepository accountRepository,
             ICurrentTenant currentTenant,
             ILogger<AccountsController> logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
            _passwordHasher = passwordHasher;
            _accountRepository = accountRepository;
            _currentTenant = currentTenant;
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
                EmailConfirmed = true,

                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,

                // MULTI-TENANT: the new account joins the clinic of the administrator provisioning
                // it. There is no other defensible answer - an administrator can only see and
                // administer their own clinic, so that is necessarily the one they are staffing.
                //
                // Inherited rather than accepted from the request body on purpose: a TenantId
                // field on RegisterDto would let an administrator of one clinic create accounts
                // inside another, which is precisely the boundary this whole change exists to draw.
                TenantId = _currentTenant.TenantId
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

            // Checked AFTER the password, deliberately. Rejecting a deactivated account before
            // verifying credentials would answer faster - and differently - for an address that
            // exists, which is the account-enumeration oracle the decoy hash above exists to close.
            // The message is the same generic one for the same reason.
            if (!user.IsActive || user.IsDeleted)
            {
                _logger.LogWarning("Login attempted against a deactivated account {UserId} from {RemoteIpAddress}.",
                    user.Id, HttpContext.Connection.RemoteIpAddress);

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

        #region Account administration

        // Administering other people's logins is an administrator's job in any reading of this
        // system, so the whole region is gated once here rather than per action.

        // GET: api/Accounts
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpGet]
        [ProducesResponseType(typeof(Pagination<AccountDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<Pagination<AccountDto>>> GetAccounts(
            [FromQuery] AccountSpecParams param, CancellationToken cancellationToken)
        {
            var isActive = param.Status?.ToLowerInvariant() switch
            {
                "active" => true,
                "inactive" => false,
                _ => (bool?)null
            };

            var (items, totalCount) = await _accountRepository.ListAsync(
                param.Search, param.Role, isActive, param.Sort, param.Skip, param.PageSize, cancellationToken);

            // An empty page is a successful result, not a missing resource - same as Doctors.
            var data = items.Select(ToDto).ToList();

            return Ok(new Pagination<AccountDto>(param.PageIndex, param.PageSize, totalCount, data));
        }

        // GET: api/Accounts/{id}
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AccountDto>> GetAccount(string id, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.FindAsync(id, cancellationToken);

            return account is null ? NotFound("Account not found.") : Ok(ToDto(account));
        }

        // POST: api/Accounts
        //
        // Register/{role} provisions one account for one purpose and answers with the minimum the
        // caller needs. This is the administration screen's create: it accepts a status and a
        // confirmed password, and answers with the row the table will render.
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpPost]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<AccountDto>> CreateAccount(CreateAccountDto model, CancellationToken cancellationToken)
        {
            var email = model.Email.Trim();
            var userName = string.IsNullOrWhiteSpace(model.UserName) ? email : model.UserName.Trim();

            if (await _accountRepository.EmailInUseAsync(email, excludeUserId: null, cancellationToken))
            {
                return Conflict(EmailTaken);
            }

            if (await _accountRepository.UserNameInUseAsync(userName, excludeUserId: null, cancellationToken))
            {
                return Conflict(UserNameTaken);
            }

            // The unique index on AspNetUsers still covers soft-deleted rows, so without this the
            // insert below would fail on a constraint violation whose message means nothing to an
            // administrator looking at an address that appears to be free.
            if (await _accountRepository.IsRetiredIdentifierAsync(email, userName, cancellationToken))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "A deleted account already uses that email address or username. "
                          + "Choose a different one, or restore the existing account."
                });
            }

            var user = new AppUser
            {
                DisplayName = model.DisplayName.Trim(),
                Email = email,
                UserName = userName,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),

                // An administrator vouched for this address - see Register.
                EmailConfirmed = true,
                IsActive = model.IsActive,
                CreatedAtUtc = DateTimeOffset.UtcNow,

                // MULTI-TENANT: inherited from the administrator creating it - see Register.
                TenantId = _currentTenant.TenantId
            };

            var created = await _userManager.CreateAsync(user, model.Password);

            if (!created.Succeeded)
            {
                return IdentityFailure(created);
            }

            var assigned = await _userManager.AddToRoleAsync(user, model.Role);

            if (!assigned.Succeeded)
            {
                // An account with no role can authenticate but do nothing, and the reason is
                // invisible from every screen. Do not leave one behind - same rule as Register.
                await _userManager.DeleteAsync(user);
                return IdentityFailure(assigned);
            }

            _logger.LogInformation("Account {UserId} created with role {Role} by {Administrator}.",
                user.Id, model.Role, User.FindFirstValue(ClaimTypes.NameIdentifier));

            var dto = ToDto(new AccountListItem(user, model.Role));

            return CreatedAtAction(nameof(GetAccount), new { id = user.Id }, dto);
        }

        // PUT: api/Accounts/{id}
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<AccountDto>> UpdateAccount(
            string id, UpdateAccountDto model, CancellationToken cancellationToken)
        {
            var existing = await _accountRepository.FindAsync(id, cancellationToken);
            if (existing is null)
            {
                return NotFound("Account not found.");
            }

            // Tracked instance: FindAsync reads AsNoTracking, and UserManager.UpdateAsync needs an
            // entity the context is following.
            var user = await _userManager.FindByIdAsync(id);
            if (user is null || user.IsDeleted)
            {
                return NotFound("Account not found.");
            }

            var email = model.Email.Trim();

            if (await _accountRepository.EmailInUseAsync(email, excludeUserId: id, cancellationToken))
            {
                return Conflict(EmailTaken);
            }

            var isSelf = string.Equals(id, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal);

            // Locking yourself out of the only administrative screen in the application is a
            // one-click, irreversible mistake that needs a second administrator to undo.
            if (isSelf && !model.IsActive)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "You cannot deactivate your own account."
                });
            }

            if (isSelf && !string.Equals(model.Role, existing.Role, StringComparison.Ordinal))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "You cannot change your own role."
                });
            }

            user.DisplayName = model.DisplayName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            user.IsActive = model.IsActive;

            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                // SetEmailAsync rather than assigning the property: it also rewrites
                // NormalizedEmail, which is the column every lookup actually matches on. Assigning
                // Email alone leaves the two out of step and the account unfindable by its new
                // address.
                var emailChanged = await _userManager.SetEmailAsync(user, email);
                if (!emailChanged.Succeeded)
                {
                    return IdentityFailure(emailChanged);
                }
            }

            var updated = await _userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                return IdentityFailure(updated);
            }

            if (!string.Equals(existing.Role, model.Role, StringComparison.Ordinal))
            {
                var roleChanged = await ReplaceRoleAsync(user, model.Role);
                if (roleChanged is not null)
                {
                    return roleChanged;
                }
            }

            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                // An administrative reset, not a change: there is no current password to present,
                // so the token is generated and immediately spent rather than emailed.
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var reset = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (!reset.Succeeded)
                {
                    return IdentityFailure(reset);
                }

                _logger.LogInformation("Password for account {UserId} reset by {Administrator}.",
                    user.Id, User.FindFirstValue(ClaimTypes.NameIdentifier));
            }

            return Ok(ToDto(new AccountListItem(user, model.Role)));
        }

        // DELETE: api/Accounts/{id}
        //
        // Soft delete. The row survives because CreatedBy/ModifiedBy stamps all over the clinical
        // tables point at it: hard-deleting the account would turn every audit entry it appears in
        // into an unresolvable identifier, which defeats the purpose of keeping an audit trail.
        [Authorize(Roles = ClinicRoles.Admin)]
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult> DeleteAccount(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user is null || user.IsDeleted)
            {
                return NotFound("Account not found.");
            }

            if (string.Equals(id, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "You cannot delete your own account."
                });
            }

            user.IsDeleted = true;
            user.DeletedAtUtc = DateTimeOffset.UtcNow;

            // Deactivated as well as deleted. IsDeleted governs what the roster shows; IsActive is
            // what Login checks. Setting only the first would leave a deleted account able to sign
            // in - invisible to the administrator who deleted it.
            user.IsActive = false;

            // Invalidates every token and cookie already issued to this account, so a session that
            // was open at the moment of deletion cannot keep working.
            var stampUpdated = await _userManager.UpdateSecurityStampAsync(user);
            if (!stampUpdated.Succeeded)
            {
                return IdentityFailure(stampUpdated);
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return IdentityFailure(result);
            }

            _logger.LogInformation("Account {UserId} deleted by {Administrator}.",
                user.Id, User.FindFirstValue(ClaimTypes.NameIdentifier));

            return NoContent();
        }

        #endregion

        #region Account helpers

        private static readonly ProblemDetails EmailTaken = new()
        {
            Status = StatusCodes.Status409Conflict,
            Title = "An account with that email address already exists."
        };

        private static readonly ProblemDetails UserNameTaken = new()
        {
            Status = StatusCodes.Status409Conflict,
            Title = "An account with that username already exists."
        };

        private static AccountDto ToDto(AccountListItem account) => new()
        {
            Id = account.User.Id,
            DisplayName = account.User.DisplayName ?? string.Empty,
            UserName = account.User.UserName ?? string.Empty,
            Email = account.User.Email ?? string.Empty,
            PhoneNumber = account.User.PhoneNumber,
            Role = account.Role,
            IsActive = account.User.IsActive,
            IsLockedOut = account.User.LockoutEnd is { } until && until > DateTimeOffset.UtcNow,
            CreatedAtUtc = account.User.CreatedAtUtc
        };

        /// <summary>
        /// Moves the account to exactly one role.
        ///
        /// Removing every existing role first, rather than only adding the new one, is what keeps
        /// AccountDto.Role honest: an account left holding both Receptionist and Admin would render
        /// as whichever the join happened to return, while authorising as both.
        /// </summary>
        private async Task<ActionResult?> ReplaceRoleAsync(AppUser user, string role)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);

            if (currentRoles.Count > 0)
            {
                var removed = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removed.Succeeded)
                {
                    return IdentityFailure(removed);
                }
            }

            var assigned = await _userManager.AddToRoleAsync(user, role);
            return assigned.Succeeded ? null : IdentityFailure(assigned);
        }

        /// <summary>
        /// Folds Identity's errors into the standard validation contract.
        ///
        /// Returning result.Errors raw leaks Identity's internal error codes in a shape nothing else
        /// in this API uses - see the same reasoning in Register.
        /// </summary>
        private ActionResult IdentityFailure(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        #endregion
    }
}
