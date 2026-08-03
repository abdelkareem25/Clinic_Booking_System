using AutoMapper;
using Clinic.Api.Controllers;
using Clinic.Api.DTOs.DoctorDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.DoctorSpec;
using Clinic.Domain.Interfaces.Specifications.PatientSpec;
using Clinic.Domain.Interfaces.Specifications.SpecParams;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Repositores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Integration tests for TODO #19 (finding H6), run against a real EF Core provider.
    ///
    /// The unit tests prove the clamping arithmetic; these prove it reaches the database. Skip(-5)
    /// and Take(-1) throw inside the provider, so only executing the query demonstrates that the
    /// 500 is genuinely gone rather than merely unlikely.
    /// </summary>
    public sealed class HostilePaginationTests : IAsyncLifetime
    {
        private const int SeededDoctors = 8;

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

            for (var i = 0; i < SeededDoctors; i++)
                context.Doctors.Add(new Doctor { Name = $"Dr. {i:00}", Specialization = "Cardiology" });

            await context.SaveChangesAsync();
        }

        private ClinicDbContext NewContext() => new(_options);

        private static Pagination<GetDoctorDto> Unwrap(ActionResult<Pagination<GetDoctorDto>> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<Pagination<GetDoctorDto>>(ok.Value);
        }

        public static TheoryData<int, int> HostileInputs() => new()
        {
            { 0, 5 },        // ?pageIndex=0    -> Skip(-5)
            { -1, 5 },
            { -100, 5 },     // ?pageIndex=-100 -> Skip(-505)
            { 1, 0 },        // ?pageSize=0     -> Take(0)
            { 1, -1 },       // ?pageSize=-1    -> Take(-1)
            { 0, 0 },
            { -5, -5 },
            { int.MinValue, int.MinValue }
        };

        [Theory]
        [MemberData(nameof(HostileInputs))]
        public async Task Hostile_Paging_Values_Do_Not_Throw(int pageIndex, int pageSize)
        {
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            // Before the fix these reached the provider as Skip(-n) / Take(-n) and surfaced as 500s.
            var page = Unwrap(await sut.GetAll(new DoctorSpecParams { PageIndex = pageIndex, PageSize = pageSize }));

            Assert.NotEmpty(page.Data);
            Assert.Equal(SeededDoctors, page.Count);
        }

        [Fact]
        public async Task A_Zero_Page_Size_No_Longer_Returns_An_Endpoint_That_Is_Always_Empty()
        {
            // Take(0) did not throw - it quietly returned nothing, forever, for every caller.
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = Unwrap(await sut.GetAll(new DoctorSpecParams { PageSize = 0 }));

            Assert.NotEmpty(page.Data);
        }

        [Fact]
        public async Task A_Page_Index_Of_Zero_Returns_The_First_Page()
        {
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var zeroth = Unwrap(await sut.GetAll(new DoctorSpecParams { PageIndex = 0, PageSize = 3 }));
            var first = Unwrap(await sut.GetAll(new DoctorSpecParams { PageIndex = 1, PageSize = 3 }));

            Assert.Equal(first.Data.Select(d => d.Id), zeroth.Data.Select(d => d.Id));
        }

        [Fact]
        public async Task An_Excessive_Page_Size_Cannot_Be_Used_To_Pull_The_Whole_Table()
        {
            // The cap is a resource control as much as a correctness one.
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = Unwrap(await sut.GetAll(new DoctorSpecParams { PageSize = int.MaxValue }));

            Assert.True(page.PageSize <= 20);
        }

        [Fact]
        public async Task A_Page_Beyond_The_End_Returns_An_Empty_Page_With_The_Real_Total()
        {
            // Over-scrolling is legitimate: the client learns the true Count and can navigate back.
            // This asserted a 404 until TODO #20 (finding H7) corrected it.
            await using var context = NewContext();
            var sut = new DoctorsController(new UnitOfWork(context), _mapper);

            var page = Unwrap(await sut.GetAll(new DoctorSpecParams { PageIndex = 99, PageSize = 5 }));

            Assert.Empty(page.Data);
            Assert.Equal(SeededDoctors, page.Count);
        }

        [Fact]
        public void Every_Specification_Derives_Skip_From_PaginationParams()
        {
            // The offset used to be recomputed as (PageIndex - 1) * PageSize in four separate
            // specifications. One definition means the clamping provably protects all of them.
            var offenders = Directory
                .EnumerateFiles(SpecificationSourceRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("PaginationParams.cs", StringComparison.Ordinal))
                .Where(path => File.ReadAllText(path)
                                   .Replace(" ", string.Empty)
                                   .Contains("PageIndex-1", StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "These specifications compute the page offset themselves instead of using " +
                "PaginationParams.Skip: " + string.Join(", ", offenders));
        }

        private static string SpecificationSourceRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Clinic.Domain")))
                directory = directory.Parent;

            Assert.True(directory is not null, "Could not locate the solution root.");
            return Path.Combine(directory!.FullName, "Clinic.Domain", "Interfaces", "Specifications");
        }

        [Fact]
        public async Task Patient_Paging_Is_Clamped_Too()
        {
            // PaginationParams is the shared base, so the fix must hold for every SpecParams type.
            await using var context = NewContext();
            context.Patients.Add(new Patient
            {
                Name = "Sara", Phone = "0100", Gender = "Female", DateOfBirth = new DateTime(1990, 1, 1)
            });
            await context.SaveChangesAsync();

            var repository = new GenericRepository<Patient>(context);
            var spec = new PatientSpecification(new PatientSpecParams { PageIndex = -3, PageSize = -3 });

            Assert.NotEmpty(await repository.ListAsync(spec));
        }

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
