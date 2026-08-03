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
    /// Tests for TODO #18 (finding H1), applied to every paginated endpoint rather than only the one
    /// that was broken.
    ///
    /// DoctorsController counted with the PAGINATED specification, so OFFSET/FETCH was applied
    /// before COUNT(*) and the total came back clamped to PageSize. A client sees one page and
    /// everything past it is unreachable. ScheduleController had the identical defect (fixed in
    /// TODO #7); Patients and Appointments happened to be correct. Testing all four keeps them so.
    /// </summary>
    public sealed class PaginationTotalCountTests : IAsyncLifetime
    {
        private const int SeededCount = 12;
        private const int PageSize = 5;

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

            var doctor = new Doctor { Name = "Dr. Anchor", Specialization = "General" };
            var patient = new Patient
            {
                Name = "Anchor Patient", Phone = "01000000000", Gender = "Female",
                DateOfBirth = new DateTime(1990, 1, 1)
            };
            context.Doctors.Add(doctor);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            for (var i = 0; i < SeededCount; i++)
            {
                context.Doctors.Add(new Doctor { Name = $"Dr. {i:00}", Specialization = "Cardiology" });
                context.Patients.Add(new Patient
                {
                    Name = $"Patient {i:00}", Phone = $"0100000{i:00}", Gender = "Female",
                    DateOfBirth = new DateTime(1990, 1, 1)
                });
                context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = doctor.Id, DayOfWeek = (WeekDay)(i % 7),
                    StartTime = TimeSpan.FromHours(8 + (i % 5)), EndTime = TimeSpan.FromHours(12 + (i % 5))
                });

                var startsAt = new DateTime(2026, 8, 3, 9, 0, 0).AddMinutes(i * 30);
                context.Appointments.Add(new Appointment
                {
                    DoctorId = doctor.Id, PatientId = patient.Id,
                    AppointmentDate = startsAt, StartTime = startsAt, EndTime = startsAt.AddMinutes(30)
                });
            }
            await context.SaveChangesAsync();
        }

        private ClinicDbContext NewContext() => new(_options);

        #region Every paginated endpoint reports the true total

        [Fact]
        public async Task Doctors_Reports_The_Total_Not_The_Page_Size()
        {
            // The endpoint this finding is about. Note the extra anchor doctor: 13 in total.
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = Unwrap<GetDoctorDto>(
                await sut.GetAll(new DoctorSpecParams { PageIndex = 1, PageSize = PageSize }));

            Assert.Equal(PageSize, page.Data.Count);
            Assert.Equal(SeededCount + 1, page.Count);
            Assert.True(page.Count > page.PageSize);
        }

        [Fact]
        public async Task Patients_Reports_The_Total_Not_The_Page_Size()
        {
            await using var context = NewContext();
            var sut = new PatientsController(_mapper, new UnitOfWork(context));

            var page = Unwrap<GetPatientDto>(
                await sut.GetAll(new PatientSpecParams { PageIndex = 1, PageSize = PageSize }));

            Assert.Equal(PageSize, page.Data.Count);
            Assert.Equal(SeededCount + 1, page.Count);
        }

        [Fact]
        public async Task Schedules_Report_The_Total_Not_The_Page_Size()
        {
            await using var context = NewContext();
            var sut = new ScheduleController(new UnitOfWork(context), _mapper);

            var page = Unwrap<DoctorScheduleDto>(
                await sut.GetSchedules(new DoctorScheduleSpecParams { PageIndex = 1, PageSize = PageSize }));

            Assert.Equal(PageSize, page.Data.Count);
            Assert.Equal(SeededCount, page.Count);
        }

        [Fact]
        public async Task Appointments_Report_The_Total_Not_The_Page_Size()
        {
            await using var context = NewContext();
            var sut = new AppointmentsController(
                _mapper, new AppointmentRepository(context), new UnitOfWork(context));

            var page = Unwrap<AppointmentDto>(
                await sut.GetAllAppointments(new AppointmentSpecParams { PageIndex = 1, PageSize = PageSize }));

            Assert.Equal(PageSize, page.Data.Count);
            Assert.Equal(SeededCount, page.Count);
        }

        #endregion

        #region Consequences of getting it wrong

        [Fact]
        public async Task Later_Doctor_Pages_Are_Reachable()
        {
            // The user-visible symptom: with Count clamped to PageSize a paginator renders one page
            // and every record past it becomes unreachable through the UI.
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var first = Unwrap<GetDoctorDto>(
                await sut.GetAll(new DoctorSpecParams { PageIndex = 1, PageSize = PageSize }));
            var third = Unwrap<GetDoctorDto>(
                await sut.GetAll(new DoctorSpecParams { PageIndex = 3, PageSize = PageSize }));

            Assert.Equal(first.Count, third.Count);
            Assert.Equal(3, third.Data.Count);                    // 13 records, page 3 of 5 -> 3 left
            Assert.Empty(first.Data.Select(d => d.Id).Intersect(third.Data.Select(d => d.Id)));
        }

        [Fact]
        public async Task A_Doctor_Filter_Narrows_The_Total()
        {
            // The count must respect the filter as well as ignore the paging.
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = Unwrap<GetDoctorDto>(await sut.GetAll(
                new DoctorSpecParams { Specialty = "Cardiology", PageIndex = 1, PageSize = PageSize }));

            Assert.Equal(SeededCount, page.Count);                // the anchor doctor is "General"
            Assert.All(page.Data, d => Assert.Equal("Cardiology", d.Specialization));
        }

        #endregion

        #region The structural guard

        [Fact]
        public async Task CountAsync_Ignores_Pagination_Even_When_Given_A_Paginated_Specification()
        {
            // The defect was passing the wrong specification object - both are valid instances of
            // the same type, so nothing complained. CountAsync now strips paging regardless, which
            // makes the whole class of mistake unreachable rather than merely fixed once.
            await using var context = NewContext();
            var repository = new GenericRepository<Doctor>(context);

            var paginated = new DoctorSpecification(new DoctorSpecParams { PageIndex = 1, PageSize = 2 });
            Assert.True(paginated.IsPaginationEnable);

            Assert.Equal(SeededCount + 1, await repository.CountAsync(paginated));
        }

        [Fact]
        public async Task CountAsync_Still_Honours_The_Filter()
        {
            // Stripping pagination must not strip the criteria with it.
            await using var context = NewContext();
            var repository = new GenericRepository<Doctor>(context);

            var spec = new DoctorWithCountSpecification(new DoctorSpecParams { Specialty = "Cardiology" });

            Assert.Equal(SeededCount, await repository.CountAsync(spec));
        }

        [Fact]
        public async Task CountAsync_On_A_Specification_With_No_Criteria_Counts_Everything()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<Doctor>(context);

            var spec = new DoctorWithCountSpecification(new DoctorSpecParams());

            Assert.Equal(SeededCount + 1, await repository.CountAsync(spec));
        }

        #endregion

        private static Pagination<T> Unwrap<T>(ActionResult<Pagination<T>> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<Pagination<T>>(ok.Value);
        }

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
