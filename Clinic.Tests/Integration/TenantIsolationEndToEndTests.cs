using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Infrastructure.Data.Context;
using Clinic.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Tenant isolation over the REAL request pipeline, end to end.
    ///
    /// TenantIsolationTests proves the DbContext filters correctly when handed a tenant. This suite
    /// proves the other half - the part no unit test can reach - that the tenant actually travels
    /// from a claim on the request, through HttpContextCurrentTenant, through dependency injection,
    /// into the context serving that request. Every link in that chain is a place the whole scheme
    /// could silently degrade to "everyone sees nothing" or, far worse, "everyone sees everything",
    /// and only a request that goes through routing, authentication and DI exercises all of them.
    ///
    /// Real controllers, real repositories, real specifications, real AutoMapper profile, real
    /// database. Nothing is mocked: a mock here would be mocking the thing under test.
    /// </summary>
    public sealed class TenantIsolationEndToEndTests : IAsyncLifetime
    {
        private const int TenantA = Tenant.DefaultTenantId;   // 1, seeded by HasData
        private const int TenantB = 2;

        private SqliteConnection _connection = default!;
        private IHost _host = default!;

        /// <summary>A client acting as staff of tenant A - the default, so it sends no tenant header.</summary>
        private HttpClient _clinicA = default!;

        /// <summary>A client acting as staff of tenant B.</summary>
        private HttpClient _clinicB = default!;

        /// <summary>Authenticated, but carrying no tenant claim at all.</summary>
        private HttpClient _tenantless = default!;

        private int _doctorOfA;
        private int _patientOfA;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(DoctorsController).Assembly);
                        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

                        services.AddDbContext<ClinicDbContext>(o => o.UseSqlite(_connection));

                        // The REAL registrations, including HttpContextCurrentTenant. Substituting
                        // a stub here would defeat the entire purpose of this suite.
                        services.AddApplicationServices();

                        services.AddAuthentication(TestAuthHandler.SchemeName)
                                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                    TestAuthHandler.SchemeName, _ => { });
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

            using (var scope = _host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
                await context.Database.EnsureCreatedAsync();

                context.Tenants.Add(new Tenant { Id = TenantB, Name = "Second Clinic" });

                // Seeded with explicit tenants, through a context that has no ambient one - the
                // supported path for seeding, and the same one the migration's backfill represents.
                var doctorA = new Doctor { TenantId = TenantA, Name = "Dr. Aya", Specialization = "Cardiology" };
                var patientA = new Patient
                {
                    TenantId = TenantA,
                    Name = "Sara",
                    Phone = "01000000000",
                    Gender = "Female",
                    DateOfBirth = new DateTime(1995, 4, 12)
                };

                context.Doctors.Add(doctorA);
                context.Doctors.Add(new Doctor { TenantId = TenantB, Name = "Dr. Omar", Specialization = "Neurology" });
                context.Patients.Add(patientA);
                await context.SaveChangesAsync();

                _doctorOfA = doctorA.Id;
                _patientOfA = patientA.Id;
            }

            _clinicA = ClientFor(tenant: null);            // no header -> default tenant
            _clinicB = ClientFor(TenantB.ToString());
            _tenantless = ClientFor(TestAuthHandler.NoTenant);   // authenticated, but no tenant claim
        }

        public async Task DisposeAsync()
        {
            _clinicA.Dispose();
            _clinicB.Dispose();
            _tenantless.Dispose();
            await _host.StopAsync();
            _host.Dispose();
            _connection.Dispose();
        }

        private HttpClient ClientFor(string? tenant)
        {
            var client = _host.GetTestClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "isolation-tests");
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, ClinicRolesForTests);

            if (tenant is not null)
                client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenant);

            return client;
        }

        /// <summary>Admin, so role-gated actions such as appointment delete are reachable.</summary>
        private const string ClinicRolesForTests = "Admin";

        private static async Task<JsonElement> PageOf(HttpResponseMessage response)
        {
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement;
        }

        // ---------------------------------------------------------------------------------------

        /// <summary>Test 1 from the brief, over HTTP: B cannot see A's doctor in a list.</summary>
        [Fact]
        public async Task Clinic_B_Does_Not_See_Clinic_A_Doctors_In_The_List()
        {
            var page = await PageOf(await _clinicB.GetAsync("/api/Doctors"));

            var names = page.GetProperty("data").EnumerateArray()
                            .Select(d => d.GetProperty("name").GetString())
                            .ToList();

            Assert.Equal(["Dr. Omar"], names);

            // The total must be filtered too. A count that ignored the filter would leak the size
            // of another clinic's roster while correctly showing none of its rows.
            Assert.Equal(1, page.GetProperty("count").GetInt32());
        }

        /// <summary>Test 2 from the brief: fetching another clinic's record by id answers 404.</summary>
        [Fact]
        public async Task Clinic_B_Gets_404_For_Clinic_A_Doctor_By_Id()
        {
            var response = await _clinicB.GetAsync($"/api/Doctors/{_doctorOfA}");

            // 404 and not 403, deliberately: the record must be indistinguishable from one that
            // does not exist. A 403 would confirm the id is real, which is itself a disclosure.
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        /// <summary>Test 4 from the brief: B cannot update A's patient.</summary>
        [Fact]
        public async Task Clinic_B_Cannot_Update_Clinic_A_Patient()
        {
            var response = await _clinicB.PutAsJsonAsync($"/api/Patients/{_patientOfA}", new
            {
                Id = _patientOfA,
                Name = "Overwritten",
                Phone = "00000000000",
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = "Male"
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // And the record is genuinely untouched, not merely reported as missing.
            var stillMine = await PageOf(await _clinicA.GetAsync($"/api/Patients/{_patientOfA}"));
            Assert.Equal("Sara", stillMine.GetProperty("name").GetString());
        }

        /// <summary>Test 5 from the brief: B cannot delete A's doctor.</summary>
        [Fact]
        public async Task Clinic_B_Cannot_Delete_Clinic_A_Doctor()
        {
            var response = await _clinicB.DeleteAsync($"/api/Doctors/{_doctorOfA}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var survives = await _clinicA.GetAsync($"/api/Doctors/{_doctorOfA}");
            Assert.Equal(HttpStatusCode.OK, survives.StatusCode);
        }

        /// <summary>Test 6 from the brief: authenticated, but with no tenant claim.</summary>
        [Fact]
        public async Task A_Caller_With_No_Tenant_Claim_Sees_Nothing()
        {
            var page = await PageOf(await _tenantless.GetAsync("/api/Doctors"));

            Assert.Empty(page.GetProperty("data").EnumerateArray());
            Assert.Equal(0, page.GetProperty("count").GetInt32());

            Assert.Equal(HttpStatusCode.NotFound,
                (await _tenantless.GetAsync($"/api/Doctors/{_doctorOfA}")).StatusCode);
        }

        /// <summary>
        /// A record created over HTTP belongs to the caller's clinic, and to no other - proving the
        /// stamping is driven by the same claim the filtering is.
        /// </summary>
        [Fact]
        public async Task A_Doctor_Created_By_Clinic_B_Belongs_To_Clinic_B()
        {
            var created = await _clinicB.PostAsJsonAsync("/api/Doctors", new
            {
                Name = "Dr. Fresh",
                Specialization = "Dermatology",
                Schedules = Array.Empty<object>()
            });

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await PageOf(created)).GetProperty("id").GetInt32();

            Assert.Equal(HttpStatusCode.OK, (await _clinicB.GetAsync($"/api/Doctors/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _clinicA.GetAsync($"/api/Doctors/{id}")).StatusCode);
        }

        /// <summary>
        /// Clinic A must be entirely unaffected by the existence of clinic B. This is the
        /// backward-compatibility assertion: everything that worked before multi-tenancy still
        /// works for the clinic that owns the data.
        /// </summary>
        [Fact]
        public async Task Clinic_A_Still_Sees_Everything_It_Owns()
        {
            var doctors = await PageOf(await _clinicA.GetAsync("/api/Doctors"));
            Assert.Equal(1, doctors.GetProperty("count").GetInt32());

            var patients = await PageOf(await _clinicA.GetAsync("/api/Patients"));
            Assert.Equal(1, patients.GetProperty("count").GetInt32());

            Assert.Equal(HttpStatusCode.OK, (await _clinicA.GetAsync($"/api/Doctors/{_doctorOfA}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await _clinicA.GetAsync($"/api/Patients/{_patientOfA}")).StatusCode);
        }
    }
}
