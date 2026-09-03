using Clinic.Domain.Entites;
using Clinic.Domain.Entites.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
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

        /// <summary>
        /// MULTI-TENANT: overrides which clinic the request acts as. Omit it and the request acts
        /// as <see cref="Tenant.DefaultTenantId"/>, which is where every test fixture seeds - so
        /// existing tests are unaffected and an isolation test can simply send this header to
        /// become a different clinic.
        ///
        /// Send <see cref="NoTenant"/> to act as a caller with no tenant claim at all, which is
        /// what proves an authenticated request carrying no tenant can still see nothing.
        /// </summary>
        public const string TenantHeader = "X-Test-Tenant";

        /// <summary>
        /// The value meaning "issue no tenant claim".
        ///
        /// A sentinel rather than an empty header, because HttpClient DROPS a header whose value is
        /// empty - the request then arrives looking exactly like one that never set the header, and
        /// the caller silently becomes the default tenant. A test written that way passes while
        /// asserting nothing, which is worse than one that fails.
        /// </summary>
        public const string NoTenant = "none";

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

            // MULTI-TENANT: mirrors what TokenService puts in a real token. Without it the
            // authenticated principal carries no tenant, the query filters match nothing, and every
            // test that reads data would fail for a reason unrelated to what it is testing.
            //
            // An absent header means the default clinic, so existing tests are unaffected;
            // NoTenant means no claim at all.
            var requestedTenant = Request.Headers.TryGetValue(TenantHeader, out var tenant)
                ? tenant.ToString()
                : Tenant.DefaultTenantId.ToString(CultureInfo.InvariantCulture);

            if (!string.Equals(requestedTenant, NoTenant, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(requestedTenant))
            {
                claims.Add(new Claim(ClinicClaimTypes.TenantId, requestedTenant));
            }

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
