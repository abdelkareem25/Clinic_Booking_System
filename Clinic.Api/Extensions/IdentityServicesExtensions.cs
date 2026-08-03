using Clinic.Application;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Service;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Clinic.Api.Extensions
{
    public static class IdentityServicesExtensions
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection Services, IConfiguration configuration)
        {
            Services.AddScoped<ITokenService, TokenService>();

            // Bind and validate the JWT settings once, centrally. ValidateOnStart turns a missing or
            // malformed JWT section into a startup failure with a clear message, instead of a
            // 500 or a silent 401 on the first authenticated request.
            Services.AddOptions<JwtOptions>()
                    .Bind(configuration.GetSection(JwtOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

            // AddIdentityCore, NOT AddIdentity.
            //
            // AddIdentity registers the four Identity cookie handlers and, critically, sets
            //     DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme
            //     DefaultChallengeScheme    = IdentityConstants.ApplicationScheme
            // Those specific properties take precedence over DefaultScheme, so a later
            // AddAuthentication("Bearer") could not override them: [Authorize] resolved to the
            // cookie handler and never looked at the Authorization: Bearer header.
            //
            // This API is token-only, so it registers the Identity *services* without any cookie
            // handler and without any scheme defaults of its own.
            Services.AddIdentityCore<AppUser>(options =>
                    {
                        // Identity ships lockout machinery but it is inert unless the sign-in call
                        // opts in (see AccountsController.Login) AND it is enabled for the account.
                        // Stated explicitly rather than relying on framework defaults, because these
                        // numbers are a security decision someone should be able to find and review.
                        options.Lockout.AllowedForNewUsers = true;
                        options.Lockout.MaxFailedAccessAttempts = 5;
                        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                        // Login looks accounts up by email (FindByEmailAsync). Without this,
                        // Identity happily stores two accounts with the same address and the lookup
                        // silently resolves to whichever one it finds first - so a user could be
                        // unable to sign in with a password that is genuinely theirs.
                        options.User.RequireUniqueEmail = true;
                    })
                    .AddRoles<IdentityRole>()                     // must precede AddEntityFrameworkStores
                                                                  // so the IRoleStore is registered too
                    .AddSignInManager<SignInManager<AppUser>>()   // AddIdentityCore does not add this
                    .AddEntityFrameworkStores<ClinicDbContext>()
                    .AddDefaultTokenProviders();

            // TokenValidationParameters are supplied by ConfigureJwtBearerOptions, which reads the
            // same validated JwtOptions that TokenService signs with. Both halves therefore agree
            // on key, issuer, audience and algorithm by construction.
            Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

            // Set all three scheme properties explicitly. DefaultScheme alone is not sufficient:
            // whenever DefaultAuthenticateScheme / DefaultChallengeScheme are set they win, which
            // is exactly how the previous configuration silently disabled JWT.
            Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

            return Services;
        }
    }
}
