using Clinic.Domain.Entites.Identity;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests.Security
{
    /// <summary>
    /// Behaviour tests for TODO #11 (finding C8).
    ///
    /// AspNetRoles was never populated. No user could hold a role, TokenService therefore emitted no
    /// role claims, and every [Authorize(Roles = ...)] endpoint returned 403 to everyone - including
    /// the administrator. Nothing anywhere reported this: a role check against a role that does not
    /// exist simply never passes.
    /// </summary>
    public sealed class SeedRolesTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public SeedRolesTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ClinicDbContext>(o => o.UseSqlite(_connection));
            services.AddIdentityCore<AppUser>(o => o.User.RequireUniqueEmail = true)
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<ClinicDbContext>();

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ClinicDbContext>().Database.EnsureCreated();
        }

        private UserManager<AppUser> Users =>
            _provider.CreateScope().ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        private RoleManager<IdentityRole> Roles =>
            _provider.CreateScope().ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        private static IConfiguration Config(params (string Key, string? Value)[] values) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
                .Build();

        private static IConfiguration AdminConfig() => Config(
            ("Seed:AdminEmail", "admin@clinic.local"),
            ("Seed:AdminPassword", "A-Strong-Passw0rd!"));

        [Fact]
        public async Task Every_Declared_Role_Is_Created()
        {
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, Config());

            var roles = Roles;
            foreach (var role in ClinicRoles.All)
                Assert.True(await roles.RoleExistsAsync(role), $"Role '{role}' was not created.");
        }

        [Fact]
        public async Task Roles_Are_Seeded_Even_Without_Administrator_Credentials()
        {
            // Roles are not secrets and the application needs them to exist regardless of whether an
            // administrator account has been configured for this environment.
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, Config());

            Assert.Equal(ClinicRoles.All.Length, Roles.Roles.Count());
            Assert.Empty(Users.Users.ToList());
        }

        [Fact]
        public async Task Seeding_Twice_Does_Not_Duplicate_Roles()
        {
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, Config());
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, Config());

            Assert.Equal(ClinicRoles.All.Length, Roles.Roles.Count());
        }

        [Fact]
        public async Task An_Existing_Role_Is_Left_Alone()
        {
            var roles = Roles;
            await roles.CreateAsync(new IdentityRole(ClinicRoles.Doctor));
            var originalId = (await roles.FindByNameAsync(ClinicRoles.Doctor))!.Id;

            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, Config());

            Assert.Equal(originalId, (await Roles.FindByNameAsync(ClinicRoles.Doctor))!.Id);
            Assert.Equal(ClinicRoles.All.Length, Roles.Roles.Count());
        }

        [Fact]
        public async Task The_Seeded_Administrator_Holds_The_Admin_Role()
        {
            // The whole point: without this, [Authorize(Roles = "Admin,Doctor")] is dead code.
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, AdminConfig());

            var users = Users;
            var admin = await users.FindByEmailAsync("admin@clinic.local");

            Assert.NotNull(admin);
            Assert.True(await users.IsInRoleAsync(admin!, ClinicRoles.Admin));
            Assert.Contains(ClinicRoles.Admin, await users.GetRolesAsync(admin!));
        }

        [Fact]
        public async Task The_Administrator_Is_Not_Given_Every_Role()
        {
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, AdminConfig());

            var users = Users;
            var admin = await users.FindByEmailAsync("admin@clinic.local");

            Assert.False(await users.IsInRoleAsync(admin!, ClinicRoles.Patient));
            Assert.False(await users.IsInRoleAsync(admin!, ClinicRoles.Doctor));
            Assert.Single(await users.GetRolesAsync(admin!));
        }

        [Fact]
        public async Task Re_Seeding_Does_Not_Duplicate_The_Role_Assignment()
        {
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, AdminConfig());
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, AdminConfig());

            var users = Users;
            var admin = await users.FindByEmailAsync("admin@clinic.local");

            Assert.Single(await users.GetRolesAsync(admin!));
        }

        [Fact]
        public async Task An_Administrator_That_Predates_Role_Seeding_Is_Repaired()
        {
            // Anyone upgrading an existing database already has an admin row with no role. Seeding
            // must put them back in the Admin role rather than leaving them unable to do anything.
            var users = Users;
            await users.CreateAsync(
                new AppUser
                {
                    DisplayName = "Legacy Admin", Email = "admin@clinic.local",
                    UserName = "admin@clinic.local", EmailConfirmed = true
                },
                "A-Strong-Passw0rd!");

            Assert.Empty(await Users.GetRolesAsync((await Users.FindByEmailAsync("admin@clinic.local"))!));

            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, AdminConfig());

            var repaired = await Users.FindByEmailAsync("admin@clinic.local");
            Assert.True(await Users.IsInRoleAsync(repaired!, ClinicRoles.Admin));
            Assert.Single(Users.Users.ToList());          // repaired, not duplicated
        }

        [Fact]
        public async Task Roles_Are_Seeded_Before_The_Administrator_Needs_Them()
        {
            // Ordering matters: AddToRoleAsync against a non-existent role fails. A single call must
            // leave a fully usable administrator.
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, AdminConfig());

            var users = Users;
            var admin = await users.FindByEmailAsync("admin@clinic.local");

            Assert.True(await Roles.RoleExistsAsync(ClinicRoles.Admin));
            Assert.True(await users.IsInRoleAsync(admin!, ClinicRoles.Admin));
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }
}
