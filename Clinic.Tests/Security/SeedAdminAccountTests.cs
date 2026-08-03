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
    /// Behaviour tests for TODO #9 (finding C11), covering the seeder.
    ///
    /// The old seeder created a fixed account with a hard-coded password unconditionally, in any
    /// environment, and discarded the IdentityResult. Seeding is now opt-in and configuration-driven.
    /// </summary>
    public sealed class SeedAdminAccountTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;

        public SeedAdminAccountTests()
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

        [Fact]
        public async Task Nothing_Is_Seeded_When_No_Credentials_Are_Configured()
        {
            // The important one: a fresh production deployment must NOT get an administrator account
            // that the whole internet can read out of the repository.
            var users = Users;

            await ClinicIdentityDbContextSeed.SeedAsync(users, Roles, Config());

            Assert.Empty(users.Users.ToList());
        }

        [Fact]
        public async Task An_Account_Is_Created_When_Credentials_Are_Configured()
        {
            var users = Users;

            await ClinicIdentityDbContextSeed.SeedAsync(users, Roles, Config(
                ("Seed:AdminEmail", "admin@clinic.local"),
                ("Seed:AdminPassword", "A-Strong-Passw0rd!")));

            var admin = await users.FindByEmailAsync("admin@clinic.local");

            Assert.NotNull(admin);
            Assert.Equal("admin@clinic.local", admin!.UserName);
            Assert.True(admin.EmailConfirmed);
            Assert.Equal("System Administrator", admin.DisplayName);
        }

        [Fact]
        public async Task The_Configured_Password_Is_Stored_As_A_Hash_And_Actually_Works()
        {
            var users = Users;

            await ClinicIdentityDbContextSeed.SeedAsync(users, Roles, Config(
                ("Seed:AdminEmail", "admin@clinic.local"),
                ("Seed:AdminPassword", "A-Strong-Passw0rd!")));

            var admin = await users.FindByEmailAsync("admin@clinic.local");

            Assert.True(await users.CheckPasswordAsync(admin!, "A-Strong-Passw0rd!"));
            Assert.False(await users.CheckPasswordAsync(admin!, "Some-Other-Passw0rd!"));
            Assert.DoesNotContain("A-Strong-Passw0rd!", admin!.PasswordHash!, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_Display_Name_Can_Be_Configured()
        {
            var users = Users;

            await ClinicIdentityDbContextSeed.SeedAsync(users, Roles, Config(
                ("Seed:AdminEmail", "admin@clinic.local"),
                ("Seed:AdminPassword", "A-Strong-Passw0rd!"),
                ("Seed:AdminDisplayName", "Clinic Owner")));

            var admin = await users.FindByEmailAsync("admin@clinic.local");

            Assert.Equal("Clinic Owner", admin!.DisplayName);
        }

        [Theory]
        [InlineData("admin@clinic.local", null)]
        [InlineData(null, "A-Strong-Passw0rd!")]
        public async Task Half_Configured_Credentials_Are_Rejected(string? email, string? password)
        {
            // A typo in one of the two keys must not quietly produce "no admin account" or an
            // account with a blank password.
            var users = Users;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ClinicIdentityDbContextSeed.SeedAsync(users, Roles, Config(
                    ("Seed:AdminEmail", email),
                    ("Seed:AdminPassword", password))));

            Assert.Contains("Incomplete seed configuration", ex.Message);
        }

        [Fact]
        public async Task A_Weak_Password_Fails_Loudly_Instead_Of_Silently()
        {
            // The old code discarded the IdentityResult, so a rejected password left the environment
            // with no account and no explanation.
            var users = Users;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ClinicIdentityDbContextSeed.SeedAsync(users, Roles, Config(
                    ("Seed:AdminEmail", "admin@clinic.local"),
                    ("Seed:AdminPassword", "a"))));

            Assert.Contains("Failed to seed the administrator account", ex.Message);
            Assert.Empty(users.Users.ToList());
        }

        [Fact]
        public async Task Seeding_Twice_Does_Not_Create_A_Duplicate()
        {
            var configuration = Config(
                ("Seed:AdminEmail", "admin@clinic.local"),
                ("Seed:AdminPassword", "A-Strong-Passw0rd!"));

            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, configuration);
            await ClinicIdentityDbContextSeed.SeedAsync(Users, Roles, configuration);

            Assert.Single(Users.Users.ToList());
        }

        [Fact]
        public async Task The_Admin_Is_Matched_By_Email_Not_By_Table_Emptiness()
        {
            // The old check was `if (!userManager.Users.Any())`, so once any user existed - a patient
            // self-registering, say - the administrator could never be seeded.
            var users = Users;
            await users.CreateAsync(
                new AppUser { DisplayName = "A Patient", Email = "patient@clinic.local", UserName = "patient@clinic.local" },
                "A-Strong-Passw0rd!");

            await ClinicIdentityDbContextSeed.SeedAsync(users, Roles, Config(
                ("Seed:AdminEmail", "admin@clinic.local"),
                ("Seed:AdminPassword", "A-Strong-Passw0rd!")));

            Assert.NotNull(await users.FindByEmailAsync("admin@clinic.local"));
            Assert.Equal(2, users.Users.Count());
        }

        public void Dispose()
        {
            _provider.Dispose();
            _connection.Dispose();
        }
    }
}
