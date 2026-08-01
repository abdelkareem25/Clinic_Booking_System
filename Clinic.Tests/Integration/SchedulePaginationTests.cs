using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.DTOs.ScheduleDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
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
    /// Integration tests for TODO #7 (finding C9), running the real controller over the real
    /// UnitOfWork, GenericRepository, SpecificationEvaluator, MappingProfile and EF Core.
    ///
    /// The seeded collection deliberately holds more rows than one page: with the old code the
    /// reported Count was clamped to PageSize, so the client saw a single page and the remaining
    /// rows were unreachable through the UI.
    /// </summary>
    public sealed class SchedulePaginationTests : IAsyncLifetime
    {
        private const int SeededSchedules = 12;

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

            var doctorA = new Doctor { Name = "Dr. Aya", Specialization = "Cardiology" };
            var doctorB = new Doctor { Name = "Dr. Omar", Specialization = "Neurology" };
            context.Doctors.AddRange(doctorA, doctorB);
            await context.SaveChangesAsync();

            // 8 for doctor A, 4 for doctor B.
            for (var i = 0; i < SeededSchedules; i++)
            {
                context.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = i < 8 ? doctorA.Id : doctorB.Id,
                    DayOfWeek = (WeekDay)(i % 7),
                    StartTime = new TimeSpan(8 + (i % 5), 0, 0),
                    EndTime = new TimeSpan(12 + (i % 5), 0, 0)
                });
            }
            await context.SaveChangesAsync();
        }

        private ClinicDbContext NewContext() => new(_options);

        private ScheduleController CreateSut(ClinicDbContext context) =>
            new(new UnitOfWork(context), _mapper);

        private static Pagination<DoctorScheduleDto> Unwrap(ActionResult<Pagination<DoctorScheduleDto>> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<Pagination<DoctorScheduleDto>>(ok.Value);
        }

        [Fact]
        public async Task Endpoint_Returns_Data_Instead_Of_Throwing()
        {
            // Mapping the specification object threw AutoMapperMappingException -> HTTP 500.
            await using var context = NewContext();
            var sut = CreateSut(context);

            var page = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams()));

            Assert.NotEmpty(page.Data);
            Assert.All(page.Data, d => Assert.NotEqual(0, d.Id));
        }

        [Fact]
        public async Task Total_Count_Exceeds_Page_Size()
        {
            // The headline defect: Count used to come back equal to PageSize.
            await using var context = NewContext();
            var sut = CreateSut(context);

            var page = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams { PageIndex = 1, PageSize = 5 }));

            Assert.Equal(5, page.Data.Count);
            Assert.Equal(SeededSchedules, page.Count);
            Assert.True(page.Count > page.PageSize,
                "Count must reflect the whole filtered collection, not the size of one page.");
        }

        [Fact]
        public async Task Later_Pages_Are_Reachable_And_Report_The_Same_Total()
        {
            await using var context = NewContext();
            var sut = CreateSut(context);

            var first = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams { PageIndex = 1, PageSize = 5 }));
            var third = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams { PageIndex = 3, PageSize = 5 }));

            Assert.Equal(SeededSchedules, first.Count);
            Assert.Equal(SeededSchedules, third.Count);
            Assert.Equal(2, third.Data.Count);                       // 12 rows, page 3 of 5 -> 2 left
            Assert.Empty(first.Data.Select(d => d.Id).Intersect(third.Data.Select(d => d.Id)));
        }

        [Fact]
        public async Task Count_Respects_The_Filter()
        {
            await using var context = NewContext();
            var sut = CreateSut(context);

            var doctorAId = (await context.Doctors.OrderBy(d => d.Id).FirstAsync()).Id;

            var page = Unwrap(await sut.GetSchedules(
                new DoctorScheduleSpecParams { DoctorId = doctorAId, PageIndex = 1, PageSize = 3 }));

            Assert.Equal(3, page.Data.Count);
            Assert.Equal(8, page.Count);                              // doctor A has 8, not all 12
            Assert.All(page.Data, d => Assert.Equal(doctorAId, d.DoctorId));
        }

        [Fact]
        public async Task Included_Doctor_Is_Projected_Into_The_Dto()
        {
            // ScheduleSpecification includes the Doctor navigation; without it DoctorName is null.
            await using var context = NewContext();
            var sut = CreateSut(context);

            var page = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams { PageSize = 5 }));

            Assert.All(page.Data, d => Assert.False(string.IsNullOrEmpty(d.DoctorName)));
        }

        [Fact]
        public async Task A_Filter_Matching_Nothing_Returns_An_Empty_Page_With_Zero_Count()
        {
            await using var context = NewContext();
            var sut = CreateSut(context);

            var page = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams { DoctorId = 99_999 }));

            Assert.Empty(page.Data);
            Assert.Equal(0, page.Count);
        }

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
