using Microsoft.AspNetCore.Authorization;

namespace Clinic.Api.Extensions
{
    public static class AuthorizationServicesExtensions
    {
        /// <summary>
        /// Makes the API deny by default.
        ///
        /// Before this, only two actions in the whole API carried [Authorize]. Everything else was
        /// anonymous, including a paginated dump of every patient's name, phone number, date of
        /// birth and gender, and anonymous create/update/delete over the same records.
        ///
        /// A fallback policy applies to every endpoint that declares NO authorization metadata of
        /// its own. That inverts the default: forgetting [Authorize] on a new endpoint now makes it
        /// unreachable rather than public, so the failure mode is a broken feature instead of a
        /// data breach. Opting out becomes a deliberate, greppable [AllowAnonymous].
        /// </summary>
        public static IServiceCollection AddClinicAuthorization(this IServiceCollection services)
        {
            services.AddAuthorizationBuilder()
                    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build());

            return services;
        }
    }
}
