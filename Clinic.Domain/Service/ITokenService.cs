using Clinic.Domain.Entites.Identity;
using Microsoft.AspNetCore.Identity;

namespace Clinic.Domain.Service
{
    public interface ITokenService
    {
        Task<string> CreateTokenAsync(AppUser user, UserManager<AppUser> userManager);
    }
}
