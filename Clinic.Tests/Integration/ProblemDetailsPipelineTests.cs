using Clinic.Api.Extensions;
using Clinic.Api.Middleware;
using Clinic.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// End-to-end tests for TODO #12 (finding C12), over the real pipeline wired exactly as
    /// Program.cs wires it.
    ///
    /// Parameterised over the environment name, because the Development/Production split is the
    /// security-relevant behaviour and a test that only ran in one of them would prove little.
    /// </summary>
    public sealed class ProblemDetailsPipelineTests
    {
        private const string SecretText = "Server=prod-sql;Password=hunter2";

        private static async Task<IHost> StartHostAsync(string environmentName) =>
            await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.UseEnvironment(environmentName);
                    webHost.ConfigureServices(services =>
                    {
                        services.AddRouting();

                        services.AddExceptionHandler<GlobalExceptionHandler>();
                        services.AddProblemDetails(options =>
                        {
                            options.CustomizeProblemDetails = context =>
                            {
                                context.ProblemDetails.Extensions["traceId"] =
                                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                            };
                        });

                        services.AddAuthentication(TestAuthHandler.SchemeName)
                                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                    TestAuthHandler.SchemeName, _ => { });
                        services.AddClinicAuthorization();
                    });
                    webHost.Configure(app =>
                    {
                        app.UseExceptionHandler();
                        app.UseStatusCodePages();

                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/boom", void () => throw new InvalidOperationException(SecretText))
                                     .AllowAnonymous();
                            endpoints.MapGet("/fine", () => "ok").AllowAnonymous();
                            endpoints.MapGet("/protected", () => "secret");
                        });
                    });
                })
                .StartAsync();

        private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrWhiteSpace(json),
                $"Expected a ProblemDetails body for {(int)response.StatusCode}, got an empty response.");
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("Production")]
        public async Task An_Unhandled_Exception_Returns_A_500_With_A_Body(string environmentName)
        {
            // Previously: 500 with an EMPTY body, which is how this review kept seeing
            // "The input does not contain any JSON tokens" instead of a diagnosis.
            using var host = await StartHostAsync(environmentName);
            using var client = host.GetTestClient();

            var response = await client.GetAsync("/boom");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await ReadProblemAsync(response);
            Assert.Equal(500, problem.GetProperty("status").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        }

        [Fact]
        public async Task Production_Does_Not_Leak_The_Exception_To_The_Caller()
        {
            using var host = await StartHostAsync("Production");
            using var client = host.GetTestClient();

            var body = await (await client.GetAsync("/boom")).Content.ReadAsStringAsync();

            Assert.DoesNotContain(SecretText, body, StringComparison.Ordinal);
            Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
            Assert.DoesNotContain("at Clinic", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Development_Does_Include_The_Exception()
        {
            using var host = await StartHostAsync("Development");
            using var client = host.GetTestClient();

            var body = await (await client.GetAsync("/boom")).Content.ReadAsStringAsync();

            Assert.Contains(SecretText, body, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_Handler_Wins_Over_The_Automatic_Developer_Exception_Page()
        {
            // WebApplication adds a developer exception page in Development. It returns text/html
            // full of source. The response must be problem+json instead.
            using var host = await StartHostAsync("Development");
            using var client = host.GetTestClient();

            var response = await client.GetAsync("/boom");

            Assert.Contains("problem+json", response.Content.Headers.ContentType?.ToString() ?? "");
            Assert.DoesNotContain("text/html", response.Content.Headers.ContentType?.ToString() ?? "");
        }

        [Fact]
        public async Task An_Unauthorized_Response_Also_Carries_A_ProblemDetails_Body()
        {
            // UseStatusCodePages gives a body to what would otherwise be a bare status code.
            using var host = await StartHostAsync("Production");
            using var client = host.GetTestClient();

            var response = await client.GetAsync("/protected");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var problem = await ReadProblemAsync(response);
            Assert.Equal(401, problem.GetProperty("status").GetInt32());
        }

        [Fact]
        public async Task A_Successful_Request_Is_Untouched()
        {
            // The error contract must not change what working endpoints return.
            using var host = await StartHostAsync("Production");
            using var client = host.GetTestClient();

            var response = await client.GetAsync("/fine");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("ok", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Every_Error_Response_Carries_A_TraceId()
        {
            using var host = await StartHostAsync("Production");
            using var client = host.GetTestClient();

            foreach (var url in new[] { "/boom", "/protected" })
            {
                var problem = await ReadProblemAsync(await client.GetAsync(url));

                Assert.True(problem.TryGetProperty("traceId", out var traceId),
                    $"{url} produced a ProblemDetails with no traceId.");
                Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
            }
        }
    }
}
