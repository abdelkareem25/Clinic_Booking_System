using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Clinic.Tests.TestSupport
{
    /// <summary>
    /// A stand-in authentication scheme for tests that are not about JWT validation.
    ///
    /// A request carrying the <see cref="UserHeader"/> header is authenticated as that user, with any
    /// roles listed in <see cref="RolesHeader"/>. A request without it is left unauthenticated, so a
    /// single host can exercise both the anonymous and the authenticated path.
    ///
    /// Real token validation is covered by JwtEndToEndTests; using it here too would couple every
    /// routing and binding test to the signing configuration.
    /// </summary>
    public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string UserHeader = "X-Test-User";
        public const string RolesHeader = "X-Test-Roles";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrWhiteSpace(user))
                return Task.FromResult(AuthenticateResult.NoResult());   // stays anonymous

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.ToString()),
                new(ClaimTypes.NameIdentifier, user.ToString())
            };

            if (Request.Headers.TryGetValue(RolesHeader, out var roles))
            {
                claims.AddRange(roles.ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(role => new Claim(ClaimTypes.Role, role)));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
