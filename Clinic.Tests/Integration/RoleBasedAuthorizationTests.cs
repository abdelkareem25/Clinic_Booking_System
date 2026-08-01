using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Service;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Net;
using System.Net.Http.Headers;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// The closing of the loop for TODO #11 (finding C8).
    ///
    /// Everything real: the seeder creates the roles and the administrator, TokenService issues a
    /// token for that administrator, the JWT bearer handler validates it, and the token is presented
    /// to the genuine [Authorize(Roles = "Admin,Doctor")] attribute on AppointmentsController.
    ///
    /// Before this item those two endpoints returned 403 to every caller in existence, because
    /// AspNetRoles was empty and no user could hold a role.
    /// </summary>
    public sealed class RoleBasedAuthorizationTests : IAsyncLifetime
    {
        private const string AdminEmail = "admin@clinic.local";
        private const string AdminPassword = "A-Strong-Passw0rd!";

        private SqliteConnection _connection = default!;
        private IHost _host = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = "role-integration-test-key-long-enough-hs256",
                    ["JWT:Issuer"] = "https://clinic.test",
                    ["JWT:Audience"] = "ClinicApiUsers",
                    ["JWT:ExpireInDays"] = "1",
                    ["Seed:AdminEmail"] = AdminEmail,
                    ["Seed:AdminPassword"] = AdminPassword
                })
                .Build();

            var appointments = new Mock<IAppointmentRepository>();
            appointments.Setup(r => r.GetByIdAsync(1))
                        .ReturnsAsync(new Appointment { Id = 1, DoctorId = 1, PatientId = 2 });
            appointments.Setup(r => r.DeleteAsync(It.IsAny<Appointment>())).Returns(Task.CompletedTask);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddSingleton<IConfiguration>(configuration);
                        services.AddDbContext<ClinicIdentityDbContext>(o => o.UseSqlite(_connection));
                        services.AddControllers().AddApplicationPart(typeof(AppointmentsController).Assembly);
                        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

                        services.AddSingleton(appointments.Object);
                        services.AddSingleton(unitOfWork.Object);

                        services.AddIdentityServices(configuration);   // real JWT + Identity
                        services.AddClinicAuthorization();
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
                })
                .StartAsync();

            using var scope = _host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ClinicIdentityDbContext>()
                       .Database.EnsureCreatedAsync();

            // The real seeder, exactly as Program.cs calls it.
            await ClinicIdentityDbContextSeed.SeedAsync(
                scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                configuration);
        }

        /// <summary>Issues a real token for a user, exactly as AccountsController.Login does.</summary>
        private async Task<string> TokenForAsync(string email)
        {
            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);

            return await tokens.CreateTokenAsync(user!, users);
        }

        private async Task<HttpClient> ClientForAsync(string email)
        {
            var client = _host.GetTestClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", await TokenForAsync(email));
            return client;
        }

        private async Task<string> CreateUserAsync(string email, params string[] roles)
        {
            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var created = await users.CreateAsync(
                new AppUser { DisplayName = email, Email = email, UserName = email, EmailConfirmed = true },
                AdminPassword);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

            if (roles.Length > 0)
            {
                var assigned = await users.AddToRolesAsync((await users.FindByEmailAsync(email))!, roles);
                Assert.True(assigned.Succeeded, string.Join("; ", assigned.Errors.Select(e => e.Description)));
            }

            return email;
        }

        [Fact]
        public async Task The_Seeded_Administrator_Can_Reach_A_Role_Protected_Endpoint()
        {
            // The headline assertion: this returned 403 for every caller before roles were seeded.
            using var client = await ClientForAsync(AdminEmail);

            var response = await client.DeleteAsync("/api/Appointments/1");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task A_Doctor_Can_Also_Reach_It()
        {
            await CreateUserAsync("doctor@clinic.local", ClinicRoles.Doctor);
            using var client = await ClientForAsync("doctor@clinic.local");

            var response = await client.DeleteAsync("/api/Appointments/1");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task An_Authenticated_User_Without_The_Role_Gets_403()
        {
            // 403 rather than 401 proves authentication succeeded and only the role check refused.
            await CreateUserAsync("receptionist@clinic.local", ClinicRoles.Receptionist);
            using var client = await ClientForAsync("receptionist@clinic.local");

            var response = await client.DeleteAsync("/api/Appointments/1");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task A_User_With_No_Roles_At_All_Gets_403()
        {
            await CreateUserAsync("nobody@clinic.local");
            using var client = await ClientForAsync("nobody@clinic.local");

            var response = await client.DeleteAsync("/api/Appointments/1");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task An_Anonymous_Caller_Still_Gets_401()
        {
            using var client = _host.GetTestClient();

            var response = await client.DeleteAsync("/api/Appointments/1");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Endpoints_Without_A_Role_Requirement_Accept_Any_Authenticated_User()
        {
            // Role seeding must not accidentally narrow endpoints that only need authentication.
            await CreateUserAsync("staff@clinic.local");
            using var client = await ClientForAsync("staff@clinic.local");

            var response = await client.GetAsync("/api/Appointments?pageSize=5");

            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }

        public async Task DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
            _connection.Dispose();
        }
    }
}
