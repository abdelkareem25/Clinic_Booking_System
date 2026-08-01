using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.DTOs.ScheduleDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Domain.Interfaces.Specifications;
using Clinic.Domain.Interfaces.Specifications.ScheduleSpec;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Clinic.Tests.Controllers
{
    /// <summary>
    /// Regression tests for TODO #7 (finding C9). The original five lines contained four defects:
    ///
    ///   1. the response mapped the specification object instead of the query result (500);
    ///   2. the query result was fetched and then discarded (wasted round trip);
    ///   3. the count specification was constructed and never used;
    ///   4. the count was taken with the PAGINATED specification, capping Count at PageSize.
    ///
    /// Each has its own test so a partial regression cannot hide.
    /// </summary>
    public sealed class ScheduleControllerGetSchedulesTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IGenericRepository<DoctorSchedule>> _repository = new();

        // The real profile - a mocked IMapper would not have caught defect 1.
        private readonly IMapper _mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

        private static readonly Doctor Doctor = new() { Id = 1, Name = "Dr. Aya", Specialization = "Cardiology" };

        private static IReadOnlyList<DoctorSchedule> TwoSchedules() =>
        [
            new() { Id = 1, DoctorId = 1, Doctor = Doctor, DayOfWeek = WeekDay.Monday,
                    StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(13, 0, 0) },
            new() { Id = 2, DoctorId = 1, Doctor = Doctor, DayOfWeek = WeekDay.Wednesday,
                    StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(18, 0, 0) }
        ];

        private ScheduleController CreateSut(
            IReadOnlyList<DoctorSchedule>? page = null, int totalItems = 2)
        {
            _repository.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<DoctorSchedule>>()))
                       .ReturnsAsync(page ?? TwoSchedules());
            _repository.Setup(r => r.CountAsync(It.IsAny<ISpecification<DoctorSchedule>>()))
                       .ReturnsAsync(totalItems);
            _unitOfWork.Setup(u => u.Repository<DoctorSchedule>()).Returns(_repository.Object);

            return new ScheduleController(_unitOfWork.Object, _mapper);
        }

        private static Pagination<DoctorScheduleDto> Unwrap(ActionResult<Pagination<DoctorScheduleDto>> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<Pagination<DoctorScheduleDto>>(ok.Value);
        }

        [Fact]
        public async Task Defect_1_Returns_The_Mapped_Schedules_Not_The_Specification()
        {
            var sut = CreateSut();

            var page = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams()));

            Assert.Equal(2, page.Data.Count);
            Assert.Equal(WeekDay.Monday, page.Data[0].WeekDay);
            Assert.Equal("Dr. Aya", page.Data[0].DoctorName);
            Assert.Equal(new TimeOnly(9, 0), page.Data[0].StartTime);
        }

        [Fact]
        public async Task Defect_2_Issues_Exactly_One_Page_Query()
        {
            var sut = CreateSut();

            await sut.GetSchedules(new DoctorScheduleSpecParams());

            _repository.Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<DoctorSchedule>>()), Times.Once);
        }

        [Fact]
        public async Task Defect_3_And_4_Counts_With_A_Non_Paginated_Specification()
        {
            ISpecification<DoctorSchedule>? countSpec = null;
            ISpecification<DoctorSchedule>? pageSpec = null;

            _repository.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<DoctorSchedule>>()))
                       .Callback<ISpecification<DoctorSchedule>>(s => pageSpec = s)
                       .ReturnsAsync(TwoSchedules());
            _repository.Setup(r => r.CountAsync(It.IsAny<ISpecification<DoctorSchedule>>()))
                       .Callback<ISpecification<DoctorSchedule>>(s => countSpec = s)
                       .ReturnsAsync(37);
            _unitOfWork.Setup(u => u.Repository<DoctorSchedule>()).Returns(_repository.Object);

            var sut = new ScheduleController(_unitOfWork.Object, _mapper);

            await sut.GetSchedules(new DoctorScheduleSpecParams { PageIndex = 1, PageSize = 2 });

            Assert.NotNull(pageSpec);
            Assert.True(pageSpec!.IsPaginationEnable, "The page query must be paginated.");

            Assert.NotNull(countSpec);
            Assert.False(countSpec!.IsPaginationEnable,
                "The count must use a non-paginated specification, otherwise Count can never exceed PageSize.");
            Assert.IsType<ScheduleCountSpecification>(countSpec);
            Assert.NotSame(pageSpec, countSpec);
        }

        [Fact]
        public async Task Total_Count_Is_Reported_Independently_Of_Page_Size()
        {
            // The page holds 2 rows but the collection has 37.
            var sut = CreateSut(page: TwoSchedules(), totalItems: 37);

            var page = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams { PageIndex = 1, PageSize = 2 }));

            Assert.Equal(2, page.Data.Count);
            Assert.Equal(37, page.Count);
            Assert.Equal(1, page.PageIndex);
            Assert.Equal(2, page.PageSize);
        }

        [Fact]
        public async Task Filters_Reach_Both_The_Page_And_The_Count_Specifications()
        {
            ISpecification<DoctorSchedule>? countSpec = null;
            _repository.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<DoctorSchedule>>()))
                       .ReturnsAsync(TwoSchedules());
            _repository.Setup(r => r.CountAsync(It.IsAny<ISpecification<DoctorSchedule>>()))
                       .Callback<ISpecification<DoctorSchedule>>(s => countSpec = s)
                       .ReturnsAsync(1);
            _unitOfWork.Setup(u => u.Repository<DoctorSchedule>()).Returns(_repository.Object);

            var sut = new ScheduleController(_unitOfWork.Object, _mapper);

            await sut.GetSchedules(new DoctorScheduleSpecParams { DoctorId = 1, WeekDay = WeekDay.Monday });

            // A count spec that dropped the filters would report the whole table.
            Assert.NotNull(countSpec!.Criteria);
            Assert.Contains("DoctorId", countSpec.Criteria!.ToString());
            Assert.Contains("DayOfWeek", countSpec.Criteria.ToString());
        }

        [Fact]
        public async Task An_Empty_Page_Still_Returns_A_Well_Formed_Envelope()
        {
            var sut = CreateSut(page: [], totalItems: 0);

            var page = Unwrap(await sut.GetSchedules(new DoctorScheduleSpecParams()));

            Assert.Empty(page.Data);
            Assert.Equal(0, page.Count);
        }
    }
}
