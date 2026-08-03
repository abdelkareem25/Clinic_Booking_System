using Clinic.Domain.Interfaces;
using System.Security.Claims;

namespace Clinic.Api.Services
{
    /// <summary>
    /// Supplies the current user's identifier from the authenticated principal.
    ///
    /// Lives in the API because it is the only layer that knows what HTTP is. Note it reads the
    /// NameIdentifier claim, which TODO #3 had to repair - a version mismatch in the IdentityModel
    /// packages was silently stripping it from every token, which would have left every audit column
    /// blank for reasons nobody could see.
    /// </summary>
    public sealed class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId =>
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
