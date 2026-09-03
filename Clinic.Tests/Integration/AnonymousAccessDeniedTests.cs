using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.Extensions;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Domain.Interfaces.Specifications;
using Clinic.Domain.Service;
using Clinic.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// End-to-end proof for TODO #10 (finding C7), over the real MVC pipeline with the real
    /// AddClinicAuthorization configuration.
    ///
    /// Every one of these URLs used to return data - or accept a mutation - with no credentials at
    /// all. For a clinical system that is unauthenticated disclosure and modification of
    /// identifiable health information, not a code-quality nit.
    /// </summary>
    public sealed class AnonymousAccessDeniedTests : IAsyncLifetime
    {
        private IHost _host = default!;
        private HttpClient _anonymous = default!;
        private HttpClient _authenticated = default!;

        public async Task InitializeAsync()
        {
            var doctors = new Mock<IGenericRepository<Doctor>>();
            doctors.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Doctor>>()))
                   .ReturnsAsync([new Doctor { Id = 1, Name = "Dr. Aya", Specialization = "Cardiology" }]);
            doctors.Setup(r => r.CountAsync(It.IsAny<ISpecification<Doctor>>())).ReturnsAsync(1);

            var patients = new Mock<IGenericRepository<Patient>>();
            patients.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Patient>>()))
                    .ReturnsAsync([new Patient { Id = 1, Name = "Sara", Phone = "0100", Gender = "Female" }]);
            patients.Setup(r => r.CountAsync(It.IsAny<ISpecification<Patient>>())).ReturnsAsync(1);

            var schedules = new Mock<IGenericRepository<DoctorSchedule>>();
            schedules.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<DoctorSchedule>>()))
                     .ReturnsAsync([]);
            schedules.Setup(r => r.CountAsync(It.IsAny<ISpecification<DoctorSchedule>>())).ReturnsAsync(0);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(u => u.Repository<Doctor>()).Returns(doctors.Object);
            unitOfWork.Setup(u => u.Repository<Patient>()).Returns(patients.Object);
            unitOfWork.Setup(u => u.Repository<DoctorSchedule>()).Returns(schedules.Object);
            unitOfWork.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            var appointments = new Mock<IAppointmentRepository>();
            appointments.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Appointment>>()))
                        .ReturnsAsync([]);
            appointments.Setup(r => r.ListAsync(It.IsAny<ISpecification<Appointment>>())).ReturnsAsync([]);

            _host = await new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddControllers().AddApplicationPart(typeof(AppointmentsController).Assembly);
                        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

                        services.AddSingleton(unitOfWork.Object);
                        services.AddSingleton(appointments.Object);

                        // AccountsController's dependencies, so a request that gets past the
                        // authorization middleware produces a real response rather than a DI failure.
                        var userStore = new Mock<IUserStore<AppUser>>();
                        var userManager = new Mock<UserManager<AppUser>>(
                            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
                        userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
                                   .ReturnsAsync(new AppUser
                                   {
                                       Id = "u1", UserName = "staff", Email = "staff@clinic.local",
                                       DisplayName = "Staff"
                                   });

                        var signInManager = new Mock<SignInManager<AppUser>>(
                            userManager.Object,
                            new Mock<IHttpContextAccessor>().Object,
                            new Mock<IUserClaimsPrincipalFactory<AppUser>>().Object,
                            null!, null!, null!, null!);
                        signInManager.Setup(m => m.CheckPasswordSignInAsync(
                                          It.IsAny<AppUser>(), It.IsAny<string>(), It.IsAny<bool>()))
                                     .ReturnsAsync(SignInResult.Success);

                        var tokenService = new Mock<ITokenService>();
                        tokenService.Setup(t => t.CreateTokenAsync(It.IsAny<AppUser>(), It.IsAny<UserManager<AppUser>>()))
                                    .ReturnsAsync("a-token");

                        services.AddSingleton(userManager.Object);
                        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
                        services.AddSingleton(signInManager.Object);
                        services.AddSingleton(tokenService.Object);
                        services.AddSingleton(new Mock<IAccountRepository>().Object);
                        services.AddSingleton<ICurrentTenant>(new StubCurrentTenant());

                        services.AddAuthentication(TestAuthHandler.SchemeName)
                                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                    TestAuthHandler.SchemeName, _ => { });

                        services.AddClinicAuthorization();   // the production configuration
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

            _anonymous = _host.GetTestClient();

            _authenticated = _host.GetTestClient();
            _authenticated.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "staff@clinic.local");
        }

        public static TheoryData<string, string> ProtectedEndpoints() => new()
        {
            { "GET",    "/api/Patients" },
            { "GET",    "/api/Patients/1" },
            { "POST",   "/api/Patients" },
            { "PUT",    "/api/Patients/1" },
            { "DELETE", "/api/Patients/1" },
            { "GET",    "/api/Doctors" },
            { "GET",    "/api/Doctors/1" },
            { "POST",   "/api/Doctors" },
            { "PUT",    "/api/Doctors/1" },
            { "DELETE", "/api/Doctors/1" },
            { "GET",    "/api/Schedule" },
            { "GET",    "/api/Schedule/1" },
            { "POST",   "/api/Schedule" },
            { "PUT",    "/api/Schedule/1" },
            { "DELETE", "/api/Schedule/1" },
            { "GET",    "/api/Appointments" },
            { "GET",    "/api/Appointments/1" },
            { "POST",   "/api/Appointments" },
            { "PUT",    "/api/Appointments/1" },
            { "DELETE", "/api/Appointments/1" },
            { "GET",    "/api/Appointments/doctor/Dr.%20Aya" },
            { "GET",    "/api/Appointments/patient/Sara" }
        };

        [Theory]
        [MemberData(nameof(ProtectedEndpoints))]
        public async Task Anonymous_Requests_Are_Rejected(string method, string url)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (method is "POST" or "PUT")
                request.Content = JsonContent.Create(new { });

            var response = await _anonymous.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task The_Patient_List_No_Longer_Leaks_Phi_To_Anonymous_Callers()
        {
            // The single worst endpoint: a paginated dump of every patient's name, phone number,
            // date of birth and gender.
            var response = await _anonymous.GetAsync("/api/Patients");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain("Sara", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0100", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_Anonymous_Mutation_Never_Reaches_The_Unit_Of_Work()
        {
            // 401 must be produced by the middleware, before any handler runs.
            var response = await _anonymous.PostAsJsonAsync("/api/Doctors",
                new { id = 0, name = "Injected", specialization = "None" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Authenticated_Requests_Still_Work()
        {
            // The fix must deny anonymous callers without breaking legitimate ones.
            var response = await _authenticated.GetAsync("/api/Doctors");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Login_Remains_Reachable_Without_A_Token()
        {
            // Necessarily anonymous - it is where a caller obtains the token everything else needs.
            var response = await _anonymous.PostAsJsonAsync("/api/Accounts/Login",
                new { email = "staff@clinic.local", password = "whatever" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("a-token", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Register_Requires_Authentication_Since_Todo_14()
        {
            // Anonymous self-registration was itself a weakness; TODO #14 closed it. Account
            // creation is now an administrator action.
            var response = await _anonymous.PostAsJsonAsync("/api/Accounts/Register", new { });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task An_Unknown_Route_Is_Challenged_Rather_Than_Answered()
        {
            // Documented consequence of a fallback policy: it applies to requests that match NO
            // endpoint too, so an unknown path answers 401 instead of 404.
            //
            // For this API that is acceptable, even mildly preferable - it stops route enumeration.
            // The thing to remember is that anything served AFTER UseAuthorization (static files, a
            // health endpoint) must opt out explicitly. Swagger is unaffected because UseSwagger
            // runs earlier in the pipeline.
            var response = await _anonymous.GetAsync("/api/NoSuchThing");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        public async Task DisposeAsync()
        {
            _anonymous.Dispose();
            _authenticated.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
