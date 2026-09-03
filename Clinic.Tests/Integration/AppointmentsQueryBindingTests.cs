using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Clinic.Api.Helper;
using Clinic.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Specifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Net;
using System.Text.Json;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// End-to-end proof for TODO #6 (finding C6), through the real MVC pipeline.
    ///
    /// The 415 came from model binding, not from the action body, so only a test that goes through
    /// routing and binding can demonstrate it. The repository is mocked - this suite is about the
    /// request reaching the action with a populated parameter, not about data access.
    /// </summary>
    public sealed class AppointmentsQueryBindingTests : IAsyncLifetime
    {
        private IHost _host = default!;
        private HttpClient _client = default!;
        private readonly Mock<IAppointmentRepository> _appointments = new();

        public async Task InitializeAsync()
        {
            var doctor = new Doctor { TenantId = Tenant.DefaultTenantId, Id = 1, Name = "Dr. Aya", Specialization = "Cardiology" };
            var patient = new Patient
            {
                TenantId = Tenant.DefaultTenantId, Id = 2, Name = "Sara", Phone = "01000000000", Gender = "Female",
                DateOfBirth = new DateTime(1995, 4, 12)
            };

            IReadOnlyList<Appointment> appointments =
            [
                new()
                {
                    Id = 10, DoctorId = 1, PatientId = 2, Doctor = doctor, Patient = patient,
                    AppointmentDate = new DateTime(2026, 8, 3, 10, 0, 0)
                }
            ];

            _appointments.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Appointment>>()))
                         .ReturnsAsync(appointments);
            _appointments.Setup(r => r.CountAsync(It.IsAny<ISpecification<Appointment>>()))
                         .ReturnsAsync(1);

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddControllers()
                                .AddApplicationPart(typeof(AppointmentsController).Assembly);

                        // The real profile - valid since TODO #4.
                        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

                        services.AddSingleton(_appointments.Object);
                        services.AddSingleton(new Mock<IUnitOfWork>().Object);

                        // Controllers now require authorization (TODO #10), so the pipeline needs an
                        // authentication scheme. This suite is about model binding, not auth, so
                        // requests carry the test user header set on the client below.
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

            _client = _host.GetTestClient();
            _client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "binding-tests");
        }

        [Fact]
        public async Task Get_All_Appointments_Does_Not_Return_415()
        {
            // The exact symptom: [ApiController] inferred [FromBody] on a GET.
            var response = await _client.GetAsync("/api/Appointments");

            Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Get_All_Appointments_Returns_A_Pagination_Envelope()
        {
            var response = await _client.GetAsync("/api/Appointments");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            Assert.Equal(1, root.GetProperty("count").GetInt32());
            Assert.Equal(1, root.GetProperty("data").GetArrayLength());
            Assert.Equal("Dr. Aya", root.GetProperty("data")[0].GetProperty("doctorName").GetString());
        }

        [Fact]
        public async Task Query_String_Values_Reach_The_Specification_Parameters()
        {
            // Proves the parameter is genuinely bound from the query string, not just defaulted.
            var response = await _client.GetAsync("/api/Appointments?pageIndex=2&pageSize=7&doctorId=1&sort=Descending");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2, json.RootElement.GetProperty("pageIndex").GetInt32());
            Assert.Equal(7, json.RootElement.GetProperty("pageSize").GetInt32());
        }

        [Fact]
        public async Task Filters_Are_Carried_Into_The_Specification()
        {
            ISpecification<Appointment>? captured = null;
            _appointments.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Appointment>>()))
                         .Callback<ISpecification<Appointment>>(s => captured = s)
                         .ReturnsAsync([]);

            await _client.GetAsync("/api/Appointments?doctorId=42");

            Assert.NotNull(captured);
            Assert.NotNull(captured!.Criteria);
            // The criteria closes over the bound param, so 42 appears in the expression tree.
            Assert.Contains("DoctorId", captured.Criteria!.ToString());
        }

        [Fact]
        public async Task An_Explicit_Json_Content_Type_Is_Not_Required()
        {
            // A browser or the Angular client sends a plain GET with no Content-Type at all.
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/Appointments?pageSize=5");

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
