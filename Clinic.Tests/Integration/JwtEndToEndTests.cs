using System.Net;
using System.Net.Http.Headers;
using Clinic.Api.Extensions;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Service;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// End-to-end proof that TODO #2 + TODO #3 together restore working authentication.
    ///
    /// A real AppUser is created through UserManager, a real token is issued by TokenService, and
    /// that token is presented over real HTTP to an endpoint protected by [Authorize]. Before these
    /// two fixes this was impossible: the cookie handler was selected (C2) and, even once the bearer
    /// handler was reachable, it had no signing key to validate against (C3).
    /// </summary>
    public sealed class JwtEndToEndTests : IAsyncLifetime
    {
        private const string Key = "integration-test-key-long-enough-for-hs256";
        private const string Issuer = "https://clinic.test";
        private const string Audience = "ClinicApiUsers";

        private SqliteConnection _connection = default!;
        private IHost _host = default!;
        private HttpClient _client = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = Key,
                    ["JWT:Issuer"] = Issuer,
                    ["JWT:Audience"] = Audience,
                    ["JWT:ExpireInDays"] = "1"
                })
                .Build();

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddSingleton<IConfiguration>(configuration);
                        services.AddDbContext<ClinicDbContext>(o => o.UseSqlite(_connection));
                        services.AddRouting();
                        services.AddAuthorization();

                        services.AddIdentityServices(configuration);
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/secure", (HttpContext ctx) =>
                                ctx.User.FindFirstValue(ClaimTypes.Name)).RequireAuthorization();

                            endpoints.MapGet("/admin-only", (HttpContext _) => "admin")
                                     .RequireAuthorization(p => p.RequireRole("Admin"));

                            endpoints.MapGet("/doctor-only", (HttpContext _) => "doctor")
                                     .RequireAuthorization(p => p.RequireRole("Doctor"));
                        });
                    });
                })
                .StartAsync();   // ValidateOnStart runs here

            using (var scope = _host.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ClinicDbContext>()
                           .Database.EnsureCreatedAsync();
            }

            _client = _host.GetTestClient();
        }

        /// <summary>Creates a real user with real roles and issues a real token for them.</summary>
        private async Task<string> IssueTokenAsync(params string[] roles)
        {
            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var rolesManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

            var email = $"user{Guid.NewGuid():N}@clinic.test";
            var user = new AppUser { DisplayName = "Test User", Email = email, UserName = email };
            var created = await users.CreateAsync(user, "Test-Passw0rd!2026");
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

            foreach (var role in roles)
            {
                if (!await rolesManager.RoleExistsAsync(role))
                    await rolesManager.CreateAsync(new IdentityRole(role));
                await users.AddToRoleAsync(user, role);
            }

            return await tokens.CreateTokenAsync(user, users);
        }

        private void Authorize(string token) =>
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        [Fact]
        public async Task A_Token_Issued_By_TokenService_Is_Accepted_By_The_Bearer_Handler()
        {
            // The headline assertion for C3. This returned 401 before the fix because the handler
            // had no IssuerSigningKey to check the signature against.
            Authorize(await IssueTokenAsync());

            var response = await _client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task The_Authenticated_Principal_Carries_The_Users_Name()
        {
            Authorize(await IssueTokenAsync());

            var body = await _client.GetStringAsync("/secure");

            Assert.Contains("@clinic.test", body);
        }

        [Fact]
        public async Task Role_Claims_Survive_The_Round_Trip_And_Drive_Authorization()
        {
            Authorize(await IssueTokenAsync("Admin"));

            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/admin-only")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/doctor-only")).StatusCode);
        }

        [Fact]
        public async Task A_User_Without_The_Role_Gets_403_Not_401()
        {
            // 403 proves authentication succeeded and only authorization failed.
            Authorize(await IssueTokenAsync());

            var response = await _client.GetAsync("/admin-only");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task A_Tampered_Token_Is_Rejected()
        {
            var token = await IssueTokenAsync();
            var segments = token.Split('.');
            segments[1] = segments[1][..^2] + (segments[1][^2] == 'A' ? 'B' : 'A') + segments[1][^1];
            Authorize(string.Join('.', segments));

            var response = await _client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task A_Token_Signed_With_Another_Key_Is_Rejected()
        {
            // Proves ValidateIssuerSigningKey is actually in force.
            var foreign = new Clinic.Application.TokenService(
                Microsoft.Extensions.Options.Options.Create(new Clinic.Application.JwtOptions
                {
                    Key = "an-entirely-different-signing-key-32-plus",
                    Issuer = Issuer,
                    Audience = Audience,
                    ExpireInDays = 1
                }));

            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { DisplayName = "X", Email = "x@clinic.test", UserName = "x@clinic.test" };
            await users.CreateAsync(user, "Test-Passw0rd!2026");

            Authorize(await foreign.CreateTokenAsync(user, users));

            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/secure")).StatusCode);
        }

        [Fact]
        public async Task An_Expired_Token_Is_Rejected()
        {
            // Proves ValidateLifetime is in force and the 30-second skew is not hiding expiry.
            var expired = new Clinic.Application.TokenService(
                Microsoft.Extensions.Options.Options.Create(new Clinic.Application.JwtOptions
                {
                    Key = Key,
                    Issuer = Issuer,
                    Audience = Audience,
                    ExpireInDays = -1        // already expired
                }));

            using var scope = _host.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { DisplayName = "Y", Email = "y@clinic.test", UserName = "y@clinic.test" };
            await users.CreateAsync(user, "Test-Passw0rd!2026");

            Authorize(await expired.CreateTokenAsync(user, users));

            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/secure")).StatusCode);
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _host.StopAsync();
            _host.Dispose();
            _connection.Dispose();
        }
    }
}
