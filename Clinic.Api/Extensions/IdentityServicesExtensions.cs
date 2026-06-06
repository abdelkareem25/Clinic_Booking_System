using Clinic.Application;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Service;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

namespace Clinic.Api.Extensions
{
    public static class IdentityServicesExtensions
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection Services)
        {
            Services.AddScoped<ITokenService, TokenService>();
            Services.AddIdentity<AppUser, IdentityRole>()
                .AddEntityFrameworkStores<ClinicIdentityDbContext>();
            Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(); // UserManager and SignInManager are registered by AddIdentity, so we don't need to register them separately.
            return Services;
        }
    }
}
