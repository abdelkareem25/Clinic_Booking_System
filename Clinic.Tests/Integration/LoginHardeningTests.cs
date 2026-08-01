using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Clinic.Domain.Entites.Identity;
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
using System.Net.Http.Json;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// End-to-end tests for TODO #13 (finding H8), over real ASP.NET Identity against a real
    /// database. Lockout is stateful - it lives in AspNetUsers.AccessFailedCount and LockoutEnd - so
    /// only a test that actually persists failed attempts proves it engages.
    ///
    /// The default limits put the rate limiter and lockout at the same threshold (5), so each test
    /// builds a host tuned to isolate the mechanism it is about. That the two are configurable at
    /// all came out of writing these tests: hardcoded thresholds made the behaviours inseparable.
    /// </summary>
    public sealed class LoginHardeningTests : IAsyncLifetime
    {
        private const string Email = "victim@clinic.local";
        private const string CorrectPassword = "A-Strong-Passw0rd!";
        private const string WrongPassword = "definitely-not-it";

        private SqliteConnection _connection = default!;
        private IHost _lockoutHost = default!;      // rate limiter effectively disabled
        private HttpClient _client = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _lockoutHost = await CreateHostAsync(permitLimit: 1000);

            using var scope = _lockoutHost.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ClinicIdentityDbContext>().Database.EnsureCreatedAsync();

            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var created = await users.CreateAsync(
                new AppUser { DisplayName = "Victim", Email = Email, UserName = Email, EmailConfirmed = true },
                CorrectPassword);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

            _client = _lockoutHost.GetTestClient();
        }

        private async Task<IHost> CreateHostAsync(int permitLimit)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = "login-hardening-test-key-long-enough-hs256",
                    ["JWT:Issuer"] = "https://clinic.test",
                    ["JWT:Audience"] = "ClinicApiUsers",
                    ["JWT:ExpireInDays"] = "1",
                    ["RateLimiting:Auth:PermitLimit"] = permitLimit.ToString()
                })
                .Build();

            return await new HostBuilder()
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
        }

        private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
            _client.PostAsJsonAsync("/api/Accounts/Login", new { email, password });

        private async Task<AppUser> ReloadUserAsync()
        {
            using var scope = _lockoutHost.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            return (await users.FindByEmailAsync(Email))!;
        }

        private int MaxFailedAttempts =>
            _lockoutHost.Services.GetRequiredService<IOptions<IdentityOptions>>()
                        .Value.Lockout.MaxFailedAccessAttempts;

        #region Lockout

        [Fact]
        public async Task Lockout_Is_Enabled_For_The_Account()
        {
            Assert.True((await ReloadUserAsync()).LockoutEnabled);
        }

        [Fact]
        public async Task A_Failed_Attempt_Is_Recorded()
        {
            // With lockoutOnFailure: false this counter never moved off zero.
            Assert.Equal(0, (await ReloadUserAsync()).AccessFailedCount);

            await LoginAsync(Email, WrongPassword);

            Assert.Equal(1, (await ReloadUserAsync()).AccessFailedCount);
        }

        [Fact]
        public async Task The_Account_Locks_After_The_Configured_Number_Of_Failures()
        {
            // Identity locks the account on the attempt that reaches the threshold, so only the
            // attempts before it answer 401.
            for (var i = 0; i < MaxFailedAttempts - 1; i++)
            {
                var response = await LoginAsync(Email, WrongPassword);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Null((await ReloadUserAsync()).LockoutEnd);
            }

            var final = await LoginAsync(Email, WrongPassword);

            Assert.Equal(HttpStatusCode.Locked, final.StatusCode);

            var user = await ReloadUserAsync();
            Assert.NotNull(user.LockoutEnd);
            Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task A_Locked_Account_Answers_423_Even_With_The_Correct_Password()
        {
            // The point of lockout: guessing must stop working entirely for a while, so an attacker
            // cannot simply keep going.
            for (var i = 0; i < MaxFailedAttempts; i++) await LoginAsync(Email, WrongPassword);

            var response = await LoginAsync(Email, CorrectPassword);

            Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
            Assert.DoesNotContain("token", await response.Content.ReadAsStringAsync(),
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task A_Successful_Login_Resets_The_Failure_Count()
        {
            await LoginAsync(Email, WrongPassword);
            await LoginAsync(Email, WrongPassword);
            Assert.Equal(2, (await ReloadUserAsync()).AccessFailedCount);

            var response = await LoginAsync(Email, CorrectPassword);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await ReloadUserAsync()).AccessFailedCount);
        }

        #endregion

        #region Account enumeration

        [Fact]
        public async Task An_Unknown_Address_Cannot_Be_Distinguished_From_A_Wrong_Password()
        {
            var unknown = await LoginAsync("nobody@clinic.local", WrongPassword);
            var wrong = await LoginAsync(Email, WrongPassword);

            Assert.Equal(unknown.StatusCode, wrong.StatusCode);
            Assert.Equal(
                await unknown.Content.ReadAsStringAsync(),
                await wrong.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Probing_An_Unknown_Address_Cannot_Lock_A_Real_Account()
        {
            await LoginAsync("nobody@clinic.local", WrongPassword);
            await LoginAsync("nobody@clinic.local", WrongPassword);

            Assert.Equal(0, (await ReloadUserAsync()).AccessFailedCount);
        }

        #endregion

        #region Rate limiting

        [Fact]
        public async Task The_Rate_Limiter_Rejects_A_Burst_Of_Attempts()
        {
            // Lockout is per-account and does nothing against password spraying across many
            // accounts, nor against the CPU cost of each hash. The limiter covers both - note every
            // address here is different, so lockout could never trigger.
            using var host = await CreateHostAsync(RateLimitingServicesExtensions.DefaultAuthPermitLimit);
            using var client = host.GetTestClient();

            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 8; i++)
            {
                var response = await client.PostAsJsonAsync("/api/Accounts/Login",
                    new { email = $"spray{i}@clinic.local", password = WrongPassword });
                statuses.Add(response.StatusCode);
            }

            Assert.Equal(RateLimitingServicesExtensions.DefaultAuthPermitLimit,
                statuses.Count(s => s == HttpStatusCode.Unauthorized));
            Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.TooManyRequests));

            await host.StopAsync();
        }

        [Fact]
        public async Task A_Rejected_Request_Tells_The_Caller_When_To_Retry()
        {
            using var host = await CreateHostAsync(RateLimitingServicesExtensions.DefaultAuthPermitLimit);
            using var client = host.GetTestClient();

            for (var i = 0; i < RateLimitingServicesExtensions.DefaultAuthPermitLimit; i++)
                await client.PostAsJsonAsync("/api/Accounts/Login",
                    new { email = $"spray{i}@clinic.local", password = WrongPassword });

            var rejected = await client.PostAsJsonAsync("/api/Accounts/Login",
                new { email = "another@clinic.local", password = WrongPassword });

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.True(rejected.Headers.Contains("Retry-After"),
                "A 429 should carry a Retry-After header so a legitimate caller knows when to return.");

            await host.StopAsync();
        }

        [Fact]
        public async Task The_Limiter_Does_Not_Block_A_Modest_Number_Of_Genuine_Logins()
        {
            // Hardening must not lock out a receptionist who mistypes once and retries.
            using var host = await CreateHostAsync(RateLimitingServicesExtensions.DefaultAuthPermitLimit);
            using var client = host.GetTestClient();

            for (var i = 0; i < 3; i++)
            {
                var response = await client.PostAsJsonAsync("/api/Accounts/Login",
                    new { email = Email, password = CorrectPassword });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            await host.StopAsync();
        }

        #endregion

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _lockoutHost.StopAsync();
            _lockoutHost.Dispose();
            _connection.Dispose();
        }
    }
}
