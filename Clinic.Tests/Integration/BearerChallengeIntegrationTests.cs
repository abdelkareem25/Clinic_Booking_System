using System.Net;
using System.Net.Http.Headers;
using Clinic.Api.Extensions;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// End-to-end proof for TODO #2 (finding C2), over real HTTP against a TestServer that composes
    /// the application's own AddIdentityServices() extension.
    ///
    /// This is the observable symptom of the bug. With the Identity cookie handler selected, an
    /// unauthenticated request to a protected endpoint produced a 302 redirect towards a login page
    /// that does not exist in this API. With the bearer handler selected it produces a 401 carrying
    /// a "WWW-Authenticate: Bearer" challenge, which is what an SPA client expects.
    /// </summary>
    public sealed class BearerChallengeIntegrationTests : IAsyncLifetime
    {
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
                    ["JWT:Key"] = "test-key-that-is-long-enough-for-hmac-sha256",
                    ["JWT:Issuer"] = "https://localhost",
                    ["JWT:Audience"] = "ClinicApiUsers",
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

                        services.AddIdentityServices(configuration); // the system under test
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/secure", (HttpContext _) => "ok").RequireAuthorization();
                            endpoints.MapGet("/open", (HttpContext _) => "ok");
                        });
                    });
                })
                .StartAsync();

            _client = _host.GetTestClient();
        }

        [Fact]
        public async Task Protected_Endpoint_Challenges_With_401_Not_A_Cookie_Redirect()
        {
            var response = await _client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            // The cookie handler answered with 302 + Location: /Account/Login?ReturnUrl=...
            Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }

        [Fact]
        public async Task Protected_Endpoint_Emits_A_Bearer_Challenge_Header()
        {
            var response = await _client.GetAsync("/secure");

            Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        }

        [Fact]
        public async Task Malformed_Bearer_Token_Is_Rejected_By_The_Jwt_Handler()
        {
            // Proves the bearer handler is the one inspecting the Authorization header.
            // (Whether a *well-formed* token validates is TODO #3 -- the signing key is not
            //  configured yet -- so this only asserts the header reaches the JWT handler.)
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "not-a-real-token");

            var response = await _client.GetAsync("/secure");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        }

        [Fact]
        public async Task Anonymous_Endpoint_Is_Still_Reachable()
        {
            var response = await _client.GetAsync("/open");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("ok", await response.Content.ReadAsStringAsync());
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
