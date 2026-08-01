using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Clinic.Domain.Entites.Identity;
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
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// End-to-end tests for TODO #14 (finding H9), over real ASP.NET Identity.
    ///
    /// Registration was anonymous, permitted duplicate email addresses, derived the username from
    /// the local part of the address (so alice@example.com and alice@other.com collided), returned
    /// raw Identity error codes, assigned no role, and handed back a bearer token.
    /// </summary>
    public sealed class RegistrationHardeningTests : IAsyncLifetime
    {
        private const string Password = "A-Strong-Passw0rd!";

        private SqliteConnection _connection = default!;
        private IHost _host = default!;
        private HttpClient _anonymous = default!;
        private HttpClient _admin = default!;
        private HttpClient _patient = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = "registration-hardening-key-long-enough-hs256",
                    ["JWT:Issuer"] = "https://clinic.test",
                    ["JWT:Audience"] = "ClinicApiUsers",
                    ["JWT:ExpireInDays"] = "1",
                    ["Seed:AdminEmail"] = "admin@clinic.local",
                    ["Seed:AdminPassword"] = Password
                })
                .Build();

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddSingleton<IConfiguration>(configuration);
                        services.AddDbContext<ClinicIdentityDbContext>(o => o.UseSqlite(_connection));
                        services.AddControllers().AddApplicationPart(typeof(AccountsController).Assembly);

                        services.AddIdentityServices(configuration);
                        services.AddClinicAuthorization();
                        services.AddClinicRateLimiting(configuration);
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseRateLimiter();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
                })
                .StartAsync();

            using var scope = _host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ClinicIdentityDbContext>().Database.EnsureCreatedAsync();

            await ClinicIdentityDbContextSeed.SeedAsync(
                scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                configuration);

            _anonymous = _host.GetTestClient();
            _admin = await ClientForAsync("admin@clinic.local");

            await CreateUserAsync("patient@clinic.local", ClinicRoles.Patient);
            _patient = await ClientForAsync("patient@clinic.local");
        }

        private async Task<HttpClient> ClientForAsync(string email)
        {
            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);

            var client = _host.GetTestClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", await tokens.CreateTokenAsync(user!, users));
            return client;
        }

        private async Task CreateUserAsync(string email, string role)
        {
            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var created = await users.CreateAsync(
                new AppUser { DisplayName = email, Email = email, UserName = email, EmailConfirmed = true },
                Password);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

            await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, role);
        }

        private static object Registration(string email, string? role = null) => new
        {
            displayName = "New Person",
            email,
            password = Password,
            phoneNumber = "01000000000",
            role
        };

        private async Task<AppUser?> FindAsync(string email)
        {
            using var scope = _host.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(email);
        }

        #region Who may register

        [Fact]
        public async Task An_Anonymous_Caller_Cannot_Create_An_Account()
        {
            var response = await _anonymous.PostAsJsonAsync("/api/Accounts/Register",
                Registration("intruder@clinic.local"));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(await FindAsync("intruder@clinic.local"));
        }

        [Fact]
        public async Task A_Non_Administrator_Cannot_Create_An_Account()
        {
            // 403 rather than 401: authenticated, but not permitted.
            var response = await _patient.PostAsJsonAsync("/api/Accounts/Register",
                Registration("escalation@clinic.local"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(await FindAsync("escalation@clinic.local"));
        }

        [Fact]
        public async Task A_Patient_Cannot_Promote_Themselves_By_Asking_For_The_Admin_Role()
        {
            var response = await _patient.PostAsJsonAsync("/api/Accounts/Register",
                Registration("selfmade@clinic.local", ClinicRoles.Admin));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task An_Administrator_Can_Create_An_Account()
        {
            var response = await _admin.PostAsJsonAsync("/api/Accounts/Register",
                Registration("nurse@clinic.local", ClinicRoles.Receptionist));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(await FindAsync("nurse@clinic.local"));
        }

        #endregion

        #region Username and uniqueness

        [Fact]
        public async Task The_Username_Is_The_Full_Email_Address()
        {
            await _admin.PostAsJsonAsync("/api/Accounts/Register", Registration("alice@example.com"));

            var user = await FindAsync("alice@example.com");

            Assert.Equal("alice@example.com", user!.UserName);
            Assert.NotEqual("alice", user.UserName);
        }

        [Fact]
        public async Task Two_People_Sharing_A_Local_Part_Can_Both_Register()
        {
            // The exact collision: both addresses used to become the username "alice", so the
            // second registration failed with an opaque duplicate-username error.
            var first = await _admin.PostAsJsonAsync("/api/Accounts/Register", Registration("alice@example.com"));
            var second = await _admin.PostAsJsonAsync("/api/Accounts/Register", Registration("alice@other.com"));

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            Assert.NotNull(await FindAsync("alice@example.com"));
            Assert.NotNull(await FindAsync("alice@other.com"));
        }

        [Fact]
        public async Task A_Duplicate_Email_Is_Rejected_With_409()
        {
            await _admin.PostAsJsonAsync("/api/Accounts/Register", Registration("dup@clinic.local"));

            var second = await _admin.PostAsJsonAsync("/api/Accounts/Register", Registration("dup@clinic.local"));

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task Unique_Email_Is_Enforced_By_Identity_Itself()
        {
            // Belt and braces: even bypassing the controller check, the store must refuse.
            var options = _host.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

            Assert.True(options.User.RequireUniqueEmail);
        }

        #endregion

        #region Roles

        [Fact]
        public async Task A_New_Account_Defaults_To_The_Least_Privileged_Role()
        {
            await _admin.PostAsJsonAsync("/api/Accounts/Register", Registration("norole@clinic.local"));

            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = await users.GetRolesAsync((await users.FindByEmailAsync("norole@clinic.local"))!);

            Assert.Equal([ClinicRoles.Patient], roles);
        }

        [Fact]
        public async Task The_Requested_Role_Is_Assigned()
        {
            // Previously no role was assigned at all, so a registered user could authenticate but
            // was refused by every role-protected endpoint.
            await _admin.PostAsJsonAsync("/api/Accounts/Register",
                Registration("doc@clinic.local", ClinicRoles.Doctor));

            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = await users.GetRolesAsync((await users.FindByEmailAsync("doc@clinic.local"))!);

            Assert.Equal([ClinicRoles.Doctor], roles);
        }

        [Fact]
        public async Task An_Unknown_Role_Is_Rejected_And_No_Account_Is_Created()
        {
            var response = await _admin.PostAsJsonAsync("/api/Accounts/Register",
                Registration("ghost@clinic.local", "Superuser"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(await FindAsync("ghost@clinic.local"));

            Assert.Contains("Superuser", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_New_Account_Can_Immediately_Log_In_And_Use_Its_Role()
        {
            // The provisioned account must actually work end to end.
            await _admin.PostAsJsonAsync("/api/Accounts/Register",
                Registration("newdoc@clinic.local", ClinicRoles.Doctor));

            var login = await _anonymous.PostAsJsonAsync("/api/Accounts/Login",
                new { email = "newdoc@clinic.local", password = Password });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("token").GetString()));
        }

        #endregion

        #region Response shape

        [Fact]
        public async Task The_Response_Does_Not_Contain_A_Token()
        {
            // Handing the administrator a token for the account they just created would let them
            // impersonate that user.
            var response = await _admin.PostAsJsonAsync("/api/Accounts/Register",
                Registration("plain@clinic.local"));

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.False(json.RootElement.TryGetProperty("token", out _));
            Assert.DoesNotContain("token", await response.Content.ReadAsStringAsync(),
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task The_Response_Reports_The_Assigned_Role()
        {
            var response = await _admin.PostAsJsonAsync("/api/Accounts/Register",
                Registration("shape@clinic.local", ClinicRoles.Receptionist));

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal("shape@clinic.local", json.RootElement.GetProperty("email").GetString());
            Assert.Equal(ClinicRoles.Receptionist, json.RootElement.GetProperty("role").GetString());
        }

        [Fact]
        public async Task A_Weak_Password_Is_Rejected_With_The_Standard_Validation_Contract()
        {
            // Note: the DTO's own regex is STRICTER than Identity's default password policy, so a
            // password reaching Identity has already satisfied it. That makes Identity's password
            // errors unreachable from here - the folding of IdentityResult.Errors into ModelState is
            // covered by AccountsControllerRegisterTests with a mocked UserManager.
            var response = await _admin.PostAsJsonAsync("/api/Accounts/Register", new
            {
                displayName = "Weak", email = "weak@clinic.local",
                password = "short", phoneNumber = "01000000000"
            });

            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("errors", body, StringComparison.Ordinal);      // ValidationProblemDetails
            Assert.Null(await FindAsync("weak@clinic.local"));
        }

        #endregion

        public async Task DisposeAsync()
        {
            _anonymous.Dispose();
            _admin.Dispose();
            _patient.Dispose();
            await _host.StopAsync();
            _host.Dispose();
            _connection.Dispose();
        }
    }
}
