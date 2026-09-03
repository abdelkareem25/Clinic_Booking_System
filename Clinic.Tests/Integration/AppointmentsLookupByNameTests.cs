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
    /// End-to-end tests for TODO #8 (finding C10), through the real MVC pipeline.
    ///
    /// Two defects, both invisible without an HTTP round trip: the route template was missing its
    /// '/' separator, and the action mapped a collection to a single DTO. The second threw
    /// AutoMapperMappingException, so the endpoint answered 500 whenever the route was hit at all.
    /// </summary>
    public sealed class AppointmentsLookupByNameTests : IAsyncLifetime
    {
        private IHost _host = default!;
        private HttpClient _client = default!;
        private readonly Mock<IAppointmentRepository> _appointments = new();

        public async Task InitializeAsync()
        {
            var doctor = new Doctor { TenantId = Tenant.DefaultTenantId, Id = 1, Name = "Dr. Aya", Specialization = "Cardiology" };
            var patient = new Patient
            {
                TenantId = Tenant.DefaultTenantId, Id = 2, Name = "Sara Ahmed", Phone = "01000000000", Gender = "Female",
                DateOfBirth = new DateTime(1995, 4, 12)
            };

            IReadOnlyList<Appointment> matches =
            [
                new() { Id = 10, DoctorId = 1, PatientId = 2, Doctor = doctor, Patient = patient,
                        AppointmentDate = new DateTime(2026, 8, 3, 10, 0, 0) },
                new() { Id = 11, DoctorId = 1, PatientId = 2, Doctor = doctor, Patient = patient,
                        AppointmentDate = new DateTime(2026, 9, 1, 9, 0, 0) }
            ];

            _appointments.Setup(r => r.ListAsync(It.IsAny<ISpecification<Appointment>>()))
                         .ReturnsAsync(matches);

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddControllers()
                                .AddApplicationPart(typeof(AppointmentsController).Assembly);
                        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
                        services.AddSingleton(_appointments.Object);
                        services.AddSingleton(new Mock<IUnitOfWork>().Object);

                        // Controllers now require authorization (TODO #10). This suite is about
                        // routing, so requests carry the test user header set on the client below.
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
            _client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "route-tests");
        }

        [Fact]
        public async Task The_Separated_Route_Is_Reachable()
        {
            var response = await _client.GetAsync("/api/Appointments/patient/Sara%20Ahmed");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task The_Old_Unseparated_Route_No_Longer_Serves_Appointments()
        {
            // "patient{patientName}" used to match this and (attempt to) return data.
            var response = await _client.GetAsync("/api/Appointments/patientSara");

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

            // It now falls through to GetById's "{id}" template. Because that template carries no
            // :int constraint, routing accepts it and model binding rejects "patientSara" -> 400.
            // Adding route constraints so this becomes a clean 404 is TODO #43.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task The_Response_Is_A_Json_Array_Not_A_Single_Object()
        {
            // Mapping the collection to a single AppointmentDto both threw and contradicted the
            // declared IReadOnlyList<AppointmentDto> return type.
            var response = await _client.GetAsync("/api/Appointments/patient/Sara%20Ahmed");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
            Assert.Equal(2, json.RootElement.GetArrayLength());
        }

        [Fact]
        public async Task Every_Matching_Appointment_Is_Returned_Fully_Mapped()
        {
            var response = await _client.GetAsync("/api/Appointments/patient/Sara%20Ahmed");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = json.RootElement.EnumerateArray().ToList();

            Assert.Collection(items,
                first =>
                {
                    Assert.Equal(10, first.GetProperty("id").GetInt32());
                    Assert.Equal("Dr. Aya", first.GetProperty("doctorName").GetString());
                    Assert.Equal("Sara Ahmed", first.GetProperty("patientName").GetString());
                },
                second =>
                {
                    Assert.Equal(11, second.GetProperty("id").GetInt32());
                    Assert.Equal("Sara Ahmed", second.GetProperty("patientName").GetString());
                });
        }

        [Fact]
        public async Task The_Name_From_The_Route_Reaches_The_Specification()
        {
            ISpecification<Appointment>? captured = null;
            _appointments.Setup(r => r.ListAsync(It.IsAny<ISpecification<Appointment>>()))
                         .Callback<ISpecification<Appointment>>(s => captured = s)
                         .ReturnsAsync([]);

            await _client.GetAsync("/api/Appointments/patient/Sara%20Ahmed");

            Assert.NotNull(captured);
            Assert.NotNull(captured!.Criteria);
            Assert.Contains("Patient.Name", captured.Criteria!.ToString());
        }

        [Fact]
        public async Task The_Sibling_Doctor_Lookup_Behaves_The_Same_Way()
        {
            // GetByDoctorName was already correct; assert the pair stays symmetric.
            var response = await _client.GetAsync("/api/Appointments/doctor/Dr.%20Aya");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
            Assert.Equal(2, json.RootElement.GetArrayLength());
        }

        [Fact]
        public async Task A_Patient_With_No_Appointments_Returns_An_Empty_Array()
        {
            _appointments.Setup(r => r.ListAsync(It.IsAny<ISpecification<Appointment>>()))
                         .ReturnsAsync([]);

            var response = await _client.GetAsync("/api/Appointments/patient/Nobody");

            // Asserted 404 until TODO #20 (finding H7). "This patient has no appointments" is a
            // true answer, not a missing resource - and it now matches GetByDoctorName.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
            Assert.Equal(0, json.RootElement.GetArrayLength());
        }

        [Fact]
        public async Task The_Empty_Response_Does_Not_Echo_The_Searched_Name_Back()
        {
            // The old 404 body was $"No appointments found for patient '{patientName}'." - an
            // unvalidated name reflected straight into the response.
            _appointments.Setup(r => r.ListAsync(It.IsAny<ISpecification<Appointment>>()))
                         .ReturnsAsync([]);

            var body = await (await _client.GetAsync("/api/Appointments/patient/Nobody")).Content.ReadAsStringAsync();

            Assert.DoesNotContain("Nobody", body, StringComparison.OrdinalIgnoreCase);
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
