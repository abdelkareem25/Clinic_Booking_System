using Clinic.Domain.Entites;
using Clinic.Domain.Entites.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Clinic.Infrastructure.Identity
{
    public static class ClinicIdentityDbContextSeed
    {
        /// <summary>
        /// Brings the identity store up to the state the application assumes: the roles exist, and
        /// an administrator exists if one has been configured.
        ///
        /// Roles are seeded unconditionally. AspNetRoles used to be empty, so no user could hold any
        /// role, TokenService emitted no role claims, and every [Authorize(Roles = ...)] endpoint
        /// returned 403 to everyone including the administrator. Nothing reported this - a role
        /// check against a role that does not exist simply never passes.
        ///
        /// The administrator is seeded only when credentials are configured, so no environment gets
        /// a well-known account by default. See TODO #9 (finding C11).
        /// </summary>
        public static async Task SeedAsync(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            await EnsureRolesAsync(roleManager);
            await EnsureAdministratorAsync(userManager, configuration);
        }

        /// <summary>Creates any missing role. Idempotent, and safe to run on every startup.</summary>
        private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in ClinicRoles.All)
            {
                if (await roleManager.RoleExistsAsync(role)) continue;

                var result = await roleManager.CreateAsync(new IdentityRole(role));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{role}': " +
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        private static async Task EnsureAdministratorAsync(
            UserManager<AppUser> userManager,
            IConfiguration configuration)
        {
            var email = configuration["Seed:AdminEmail"];
            var password = configuration["Seed:AdminPassword"];

            var hasEmail = !string.IsNullOrWhiteSpace(email);
            var hasPassword = !string.IsNullOrWhiteSpace(password);

            // Nothing configured: deliberately do nothing.
            if (!hasEmail && !hasPassword) return;

            // Half-configured is a mistake, not an intention. Say so instead of creating an account
            // with a blank credential or skipping silently.
            if (hasEmail != hasPassword)
            {
                throw new InvalidOperationException(
                    "Incomplete seed configuration: 'Seed:AdminEmail' and 'Seed:AdminPassword' must " +
                    "either both be set or both be absent.");
            }

            var admin = await userManager.FindByEmailAsync(email!);

            if (admin is null)
            {
                admin = new AppUser
                {
                    DisplayName = configuration["Seed:AdminDisplayName"] ?? "System Administrator",
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,

                    // MULTI-TENANT: the default clinic, so a freshly created database is usable
                    // the moment it starts.
                    //
                    // The alternative - leaving this null to mark a "platform" administrator -
                    // reads well but produces an administrator who signs in perfectly and then
                    // finds an entirely empty application, because a null tenant matches no row.
                    // On an EXISTING database the migration backfills every account to this same
                    // tenant, so both paths arrive at the same place.
                    TenantId = Tenant.DefaultTenantId
                };

                var created = await userManager.CreateAsync(admin, password!);

                // A seed that fails silently leaves an environment with no way in and no explanation.
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Failed to seed the administrator account: " +
                        string.Join("; ", created.Errors.Select(e => e.Description)));
                }
            }

            // Self-healing: an administrator that predates role seeding, or one whose membership was
            // removed, is put back in the Admin role rather than being left unable to do anything.
            if (await userManager.IsInRoleAsync(admin, ClinicRoles.Admin)) return;

            var assigned = await userManager.AddToRoleAsync(admin, ClinicRoles.Admin);

            if (!assigned.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to add the administrator to the '{ClinicRoles.Admin}' role: " +
                    string.Join("; ", assigned.Errors.Select(e => e.Description)));
            }
        }
    }
}
