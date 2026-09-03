using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces;
using System.Globalization;
using System.Security.Claims;

namespace Clinic.Api.Services
{
    /// <summary>
    /// Supplies the current tenant from the authenticated principal's tenant claim.
    ///
    /// The exact counterpart of <see cref="HttpContextCurrentUser"/>, and lives here for the same
    /// reason: the API is the only layer that knows what HTTP is, so it is the only layer allowed
    /// to read a claim.
    ///
    /// This is the single point at which tenant identity enters the application. Nothing else -
    /// no controller, no repository, no specification - reads the claim, which is what stops the
    /// extraction being copied around and then copied slightly wrong.
    /// </summary>
    public sealed class HttpContextCurrentTenant : ICurrentTenant
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCurrentTenant(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// The caller's tenant, or null when there is no usable tenant claim.
        ///
        /// Every failure resolves to null - no HttpContext, an anonymous request, an absent claim,
        /// a claim that is not an integer - and never to a guess or a fallback. Null means the
        /// query filter matches no row, so the caller sees nothing. That is deliberately the safe
        /// direction: a token that somehow arrives without a readable tenant produces a visibly
        /// empty application rather than a silent window into another clinic's records.
        ///
        /// InvariantCulture is not decoration. The default TryParse overload uses the CURRENT
        /// culture, so on a server whose locale groups digits the claim "1234" could be read
        /// differently from how it was written - a class of bug that appears only after deployment
        /// to a machine configured unlike the developer's.
        /// </summary>
        public int? TenantId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClinicClaimTypes.TenantId);

                return int.TryParse(claim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tenantId)
                    ? tenantId
                    : null;
            }
        }
    }
}
