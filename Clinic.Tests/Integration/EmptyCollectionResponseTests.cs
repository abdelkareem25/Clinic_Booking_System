using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.DTOs.AppointmentDto;
using Clinic.Api.DTOs.DoctorDto;
using Clinic.Api.DTOs.PatientDto;
using Clinic.Api.DTOs.ScheduleDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.AppointmentSpec;
using Clinic.Domain.Interfaces.Specifications.DoctorSpec;
using Clinic.Domain.Interfaces.Specifications.PatientSpec;
using Clinic.Domain.Interfaces.Specifications.ScheduleSpec;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Repositores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Tests for TODO #20 (finding H7), applied to every collection endpoint.
    ///
    /// A collection resource that currently contains no matching items is a SUCCESSFUL request with
    /// an empty result. 404 means "this resource does not exist", so returning it here forced the
    /// Angular client to special-case "not an error" on every list call, turned an unproductive
    /// search into a failure, and discarded the pagination metadata entirely - the client could not
    /// tell "page 5 of 3 pages" from "no data at all".
    ///
    /// The database is deliberately EMPTY: every endpoint is exercised against no data whatsoever.
    /// </summary>
    public sealed class EmptyCollectionResponseTests : IAsyncLifetime
    {
        private SqliteConnection _connection = default!;
        private DbContextOptions<ClinicDbContext> _options = default!;

        private readonly IMapper _mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;

            await using var context = NewContext();
            await context.Database.EnsureCreatedAsync();
        }

        private ClinicDbContext NewContext() => new(_options);

        private static Pagination<T> UnwrapPage<T>(ActionResult<Pagination<T>> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<Pagination<T>>(ok.Value);
        }

        #region Every paginated endpoint answers 200 with an envelope

        [Fact]
        public async Task Doctors_Returns_An_Empty_Page_Not_404()
        {
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = UnwrapPage(await sut.GetAll(new DoctorSpecParams()));

            Assert.Empty(page.Data);
            Assert.Equal(0, page.Count);
            Assert.Equal(1, page.PageIndex);
        }

        [Fact]
        public async Task Patients_Returns_An_Empty_Page_Not_404()
        {
            await using var context = NewContext();
            var sut = new PatientsController(_mapper, new UnitOfWork(context));

            var page = UnwrapPage(await sut.GetAll(new PatientSpecParams()));

            Assert.Empty(page.Data);
            Assert.Equal(0, page.Count);
        }

        [Fact]
        public async Task Appointments_Returns_An_Empty_Page_Not_404()
        {
            await using var context = NewContext();
            var sut = new AppointmentsController(
                _mapper, new AppointmentRepository(context), new UnitOfWork(context));

            var page = UnwrapPage(await sut.GetAllAppointments(new AppointmentSpecParams()));

            Assert.Empty(page.Data);
            Assert.Equal(0, page.Count);
        }

        [Fact]
        public async Task Schedules_Returns_An_Empty_Page_Not_404()
        {
            // Already correct since TODO #7; asserted so it stays that way.
            await using var context = NewContext();
            var sut = new ScheduleController(new UnitOfWork(context), _mapper);

            var page = UnwrapPage(await sut.GetSchedules(new DoctorScheduleSpecParams()));

            Assert.Empty(page.Data);
            Assert.Equal(0, page.Count);
        }

        #endregion

        #region Lookup endpoints answer 200 with an empty array

        [Fact]
        public async Task Appointments_By_Patient_Name_Returns_An_Empty_Array_Not_404()
        {
            await using var context = NewContext();
            var sut = new AppointmentsController(
                _mapper, new AppointmentRepository(context), new UnitOfWork(context));

            var response = await sut.GetByPatientName("Nobody At All");

            var ok = Assert.IsType<OkObjectResult>(response.Result);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<AppointmentDto>>(ok.Value));
        }

        [Fact]
        public async Task The_Two_Name_Lookups_Behave_Identically_When_Nothing_Matches()
        {
            // GetByDoctorName always returned 200; GetByPatientName returned 404. Two sibling
            // endpoints with opposite semantics is exactly the inconsistency this finding is about.
            await using var context = NewContext();
            var sut = new AppointmentsController(
                _mapper, new AppointmentRepository(context), new UnitOfWork(context));

            var byDoctor = await sut.GetByDoctorName("Nobody");
            var byPatient = await sut.GetByPatientName("Nobody");

            Assert.IsType<OkObjectResult>(byDoctor.Result);
            Assert.IsType<OkObjectResult>(byPatient.Result);
        }

        #endregion

        #region A filter that matches nothing is not an error

        [Fact]
        public async Task A_Search_With_No_Matches_Is_Still_A_Success()
        {
            // Searching is the common case: typing three letters that match nothing must not look
            // like a broken endpoint.
            await using var context = NewContext();
            context.Doctors.Add(new Doctor { Name = "Dr. Aya", Specialization = "Cardiology" });
            await context.SaveChangesAsync();

            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = UnwrapPage(await sut.GetAll(new DoctorSpecParams { Search = "zzzzz" }));

            Assert.Empty(page.Data);
            Assert.Equal(0, page.Count);
        }

        [Fact]
        public async Task A_Non_Empty_Search_Still_Returns_Its_Results()
        {
            // The fix must not make everything empty.
            await using var context = NewContext();
            context.Doctors.Add(new Doctor { Name = "Dr. Aya", Specialization = "Cardiology" });
            await context.SaveChangesAsync();

            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = UnwrapPage(await sut.GetAll(new DoctorSpecParams { Search = "Aya" }));

            Assert.Single(page.Data);
            Assert.Equal(1, page.Count);
        }

        #endregion

        #region 404 is still correct for a single missing resource

        [Fact]
        public async Task A_Missing_Doctor_By_Id_Is_Still_A_404()
        {
            // The distinction that matters: a collection endpoint describes a resource that exists
            // and is empty; /api/Doctors/999 describes one that does not exist.
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            Assert.IsType<NotFoundObjectResult>((await sut.GetById(999)).Result);
        }

        [Fact]
        public async Task A_Missing_Patient_By_Id_Is_Still_A_404()
        {
            await using var context = NewContext();
            var sut = new PatientsController(_mapper, new UnitOfWork(context));

            Assert.IsType<NotFoundResult>((await sut.GetById(999)).Result);
        }

        [Fact]
        public async Task A_Missing_Appointment_By_Id_Is_Still_A_404()
        {
            await using var context = NewContext();
            var sut = new AppointmentsController(
                _mapper, new AppointmentRepository(context), new UnitOfWork(context));

            Assert.IsType<NotFoundObjectResult>((await sut.GetById(999)).Result);
        }

        #endregion

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
