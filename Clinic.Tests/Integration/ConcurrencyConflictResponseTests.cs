using Clinic.Api.Middleware;
using Clinic.Domain.Entites;
using Clinic.Infrastructure.Data.Context;
using Clinic.Tests.TestSupport;
using Clinic.Infrastructure.Repositores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Closes the loop for TODO #21 (finding H18): a real optimistic-concurrency conflict, raised by
    /// a real EF Core save, travelling through the real exception middleware to a 409 ProblemDetails.
    ///
    /// GlobalExceptionHandler has mapped DbUpdateConcurrencyException to 409 since TODO #12, but
    /// until now nothing in the application could produce one - there was no concurrency token, so
    /// the exception was unreachable and the mapping was dead code.
    ///
    /// The interleaving is performed inside the test endpoint because the subject here is the
    /// pipeline, not the controller: forcing two HTTP requests to straddle each other's
    /// load-modify-save window is inherently racy and would make the test flaky.
    /// </summary>
    public sealed class ConcurrencyConflictResponseTests : IAsyncLifetime
    {
        private SqliteConnection _connection = default!;
        private DbContextOptions<ClinicDbContext> _options = default!;
        private IHost _host = default!;
        private HttpClient _client = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;

            await using (var context = new ClinicDbContext(_options, currentTenant: new StubCurrentTenant()))
            {
                await context.Database.EnsureCreatedAsync();
                context.Doctors.Add(new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Aya", Specialization = "Cardiology" });
                await context.SaveChangesAsync();
            }

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.UseEnvironment("Production");      // no exception detail in the body
                    webHost.ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddExceptionHandler<GlobalExceptionHandler>();
                        services.AddProblemDetails(options =>
                        {
                            options.CustomizeProblemDetails = context =>
                                context.ProblemDetails.Extensions["traceId"] =
                                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                        });
                    });
                    webHost.Configure(app =>
                    {
                        app.UseExceptionHandler();
                        app.UseStatusCodePages();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Reproduces two users editing the same record: both load it, one saves,
                            // then the other tries. The write goes through GenericRepository and
                            // UnitOfWork, exactly as the controllers do.
                            endpoints.MapPost("/conflict", async () =>
                            {
                                await using var firstUser = new ClinicDbContext(_options, currentTenant: new StubCurrentTenant());
                                await using var secondUser = new ClinicDbContext(_options, currentTenant: new StubCurrentTenant());

                                var firstCopy = await new GenericRepository<Doctor>(firstUser).GetByIdAsync(1);
                                var secondCopy = await new GenericRepository<Doctor>(secondUser).GetByIdAsync(1);

                                firstCopy.Specialization = "Neurology";
                                await new UnitOfWork(firstUser).CompleteAsync();

                                secondCopy.Specialization = "Dermatology";
                                await new UnitOfWork(secondUser).CompleteAsync();   // conflict

                                return Results.NoContent();
                            });

                            endpoints.MapPost("/no-conflict", async () =>
                            {
                                await using var context = new ClinicDbContext(_options, currentTenant: new StubCurrentTenant());
                                var doctor = await new GenericRepository<Doctor>(context).GetByIdAsync(1);
                                doctor.Specialization = "Oncology";
                                await new UnitOfWork(context).CompleteAsync();
                                return Results.NoContent();
                            });
                        });
                    });
                })
                .StartAsync();

            _client = _host.GetTestClient();
        }

        [Fact]
        public async Task A_Lost_Update_Is_Reported_As_409_Conflict()
        {
            // Before this item the second save simply won, returned 204, and the first user's change
            // disappeared with no error anywhere.
            var response = await _client.PostAsync("/conflict", null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task The_Conflict_Response_Is_ProblemDetails_A_Client_Can_Act_On()
        {
            var response = await _client.PostAsync("/conflict", null);

            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            Assert.Equal(409, root.GetProperty("status").GetInt32());
            Assert.Contains("modified by another user", root.GetProperty("title").GetString()!,
                StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        }

        [Fact]
        public async Task The_Conflict_Response_Leaks_No_Internals()
        {
            var body = await (await _client.PostAsync("/conflict", null)).Content.ReadAsStringAsync();

            Assert.DoesNotContain("DbUpdateConcurrencyException", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_Losing_Write_Is_Not_Applied()
        {
            await _client.PostAsync("/conflict", null);

            await using var verification = new ClinicDbContext(_options, currentTenant: new StubCurrentTenant());
            var doctor = await verification.Doctors.SingleAsync();

            Assert.Equal("Neurology", doctor.Specialization);        // the first writer's value
            Assert.NotEqual("Dermatology", doctor.Specialization);
        }

        [Fact]
        public async Task An_Uncontended_Update_Still_Succeeds()
        {
            // Concurrency control must not turn ordinary edits into conflicts.
            var response = await _client.PostAsync("/no-conflict", null);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            await using var verification = new ClinicDbContext(_options, currentTenant: new StubCurrentTenant());
            Assert.Equal("Oncology", (await verification.Doctors.SingleAsync()).Specialization);
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
