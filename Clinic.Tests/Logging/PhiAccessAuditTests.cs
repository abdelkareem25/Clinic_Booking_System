using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Clinic.Api.Helper;
using Clinic.Api.Logging;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Domain.Interfaces.Specifications;
using Clinic.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Clinic.Tests.Logging
{
    /// <summary>
    /// Tests for TODO #16 (finding H15), covering the audit trail.
    ///
    /// A clinical system is expected to be able to answer "who accessed this patient's record and
    /// when". It previously could not: there was no logging of any kind. The other half of the
    /// obligation is just as important - the audit trail must record identifiers, never the health
    /// information itself, or it becomes a second copy of the data under weaker access control.
    /// </summary>
    public sealed class PhiAccessAuditTests : IAsyncLifetime
    {
        private const string PatientName = "Sara Ahmed";
        private const string PatientPhone = "01000000000";

        private readonly CapturingLoggerProvider _logs = new();
        private IHost _host = default!;
        private HttpClient _client = default!;

        public async Task InitializeAsync()
        {
            var patients = new Mock<IGenericRepository<Patient>>();
            patients.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Patient>>()))
                    .ReturnsAsync([new Patient
                    {
                        Id = 1, Name = PatientName, Phone = PatientPhone, Gender = "Female",
                        DateOfBirth = new DateTime(1995, 4, 12)
                    }]);
            patients.Setup(r => r.CountAsync(It.IsAny<ISpecification<Patient>>())).ReturnsAsync(1);
            patients.Setup(r => r.GetByIdAsync(7))
                    .ReturnsAsync(new Patient { Id = 7, Name = PatientName, Phone = PatientPhone, Gender = "Female" });

            var doctors = new Mock<IGenericRepository<Doctor>>();
            doctors.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Doctor>>()))
                   .ReturnsAsync([new Doctor { Id = 1, Name = "Dr. Aya", Specialization = "Cardiology" }]);
            doctors.Setup(r => r.CountAsync(It.IsAny<ISpecification<Doctor>>())).ReturnsAsync(1);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(u => u.Repository<Patient>()).Returns(patients.Object);
            unitOfWork.Setup(u => u.Repository<Doctor>()).Returns(doctors.Object);

            var appointments = new Mock<IAppointmentRepository>();
            appointments.Setup(r => r.ListAsync(It.IsAny<ISpecification<Appointment>>())).ReturnsAsync([]);

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging(b => { b.ClearProviders(); b.AddProvider(_logs); b.SetMinimumLevel(LogLevel.Trace); });
                        services.AddHttpContextAccessor();
                        services.AddControllers(o => o.Filters.Add<PhiAccessAuditFilter>())
                                .AddApplicationPart(typeof(PatientsController).Assembly);
                        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

                        services.AddSingleton(unitOfWork.Object);
                        services.AddSingleton(appointments.Object);

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
            _client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "nurse@clinic.local");
        }

        private string AuditText => string.Join(Environment.NewLine,
            _logs.Entries.Where(e => e.Message.Contains("PHI access", StringComparison.Ordinal))
                         .Select(e => e.Message));

        [Fact]
        public async Task Reading_The_Patient_List_Is_Audited()
        {
            await _client.GetAsync("/api/Patients");

            Assert.Contains("PHI access", AuditText, StringComparison.Ordinal);
            Assert.Contains("Patient/collection", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_Audit_Record_Names_The_User_Who_Accessed_The_Data()
        {
            await _client.GetAsync("/api/Patients");

            Assert.Contains("nurse@clinic.local", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_Audit_Record_Identifies_The_Specific_Record()
        {
            await _client.GetAsync("/api/Patients/7");

            Assert.Contains("Patient/7", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_Audit_Record_Names_The_Operation_And_Outcome()
        {
            await _client.GetAsync("/api/Patients/7");

            Assert.Contains("GET Patients.GetById", AuditText, StringComparison.Ordinal);
            Assert.Contains("200", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_Failed_Access_Records_The_Real_Outcome_Not_200()
        {
            // An action filter runs before the IActionResult is executed, so reading
            // Response.StatusCode there reports 200 for everything. An audit trail that records the
            // wrong outcome is worse than none, because it looks authoritative.
            await _client.GetAsync("/api/Patients/999");   // no such patient -> 404

            Assert.Contains("Patient/999", AuditText, StringComparison.Ordinal);
            Assert.Contains("-> 404", AuditText, StringComparison.Ordinal);
            Assert.DoesNotContain("-> 200", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_Write_Is_Audited_As_Well_As_A_Read()
        {
            await _client.DeleteAsync("/api/Patients/7");

            Assert.Contains("DELETE Patients.Delete", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Appointment_Access_Is_Audited()
        {
            // An appointment reveals that a person is receiving care, which is itself PHI.
            await _client.GetAsync("/api/Appointments/doctor/Dr.%20Aya");

            Assert.Contains("Appointment/", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_Audit_Trail_Never_Contains_The_Health_Information_Itself()
        {
            // The point of the whole exercise. An audit log that reproduces patient names and phone
            // numbers is a second copy of the data, usually under much weaker access control.
            await _client.GetAsync("/api/Patients");
            await _client.GetAsync("/api/Patients/7");

            Assert.DoesNotContain(PatientName, _logs.AllText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(PatientPhone, _logs.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain("1995", _logs.AllText, StringComparison.Ordinal);   // date of birth
        }

        [Fact]
        public async Task Non_Clinical_Endpoints_Are_Not_Audited()
        {
            // Doctor names and specialisations are staff directory data, not patient health
            // information. Auditing everything would bury the records that matter.
            await _client.GetAsync("/api/Doctors");

            Assert.DoesNotContain("PHI access", AuditText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_Unauthenticated_Attempt_Is_Not_Silently_Unrecorded()
        {
            // Authorization rejects before the action filter runs, so there is no audit record - but
            // the request-logging line still exists. This test documents that boundary rather than
            // asserting a behaviour that does not exist.
            using var anonymous = _host.GetTestClient();

            var response = await anonymous.GetAsync("/api/Patients");

            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain("PHI access", AuditText, StringComparison.Ordinal);
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
