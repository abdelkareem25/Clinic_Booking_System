using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications;
using Clinic.Domain.Interfaces.Specifications.DoctorSpec;
using Clinic.Domain.Interfaces.Specifications.ScheduleSpec;
using Clinic.Infrastructure.Data.Context;
using Clinic.Tests.TestSupport;
using Clinic.Infrastructure.Repositores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Tests for TODO #22 (finding H20), asserted against the SQL the evaluator actually generates
    /// and against the rows it actually returns.
    ///
    /// The instability this fixes is the kind that never reproduces on demand: ordering by a
    /// non-unique column leaves the database free to return equal rows in any order, and it need not
    /// choose the same order twice. Asserting on the generated SQL is therefore the honest test -
    /// a behavioural one can pass by luck on a small table.
    /// </summary>
    public sealed class SpecificationEvaluatorTests : IAsyncLifetime
    {
        private const int SeededDoctors = 12;

        private SqliteConnection _connection = default!;
        private DbContextOptions<ClinicDbContext> _options = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;

            await using var context = NewContext();
            await context.Database.EnsureCreatedAsync();

            // Every doctor shares a name, so the sort column has no discriminating power at all.
            // Without a tie-breaker the database may order these however it likes.
            for (var i = 0; i < SeededDoctors; i++)
                context.Doctors.Add(new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Same Name", Specialization = "Cardiology" });

            await context.SaveChangesAsync();
        }

        private ClinicDbContext NewContext() => new(_options, currentTenant: new StubCurrentTenant());

        private string SqlFor(ISpecification<Doctor> spec)
        {
            using var context = NewContext();
            return SpecificationEvalutor<Doctor>.GetQuery(context.Set<Doctor>(), spec).ToQueryString();
        }

        #region The stable tie-breaker

        [Fact]
        public void An_Ordered_Query_Always_Ends_With_The_Primary_Key()
        {
            var sql = SqlFor(new DoctorSpecification(new DoctorSpecParams { Sort = "nameAsc" }));

            var orderBy = sql[sql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];

            Assert.Contains("\"Name\"", orderBy, StringComparison.Ordinal);
            Assert.Contains("\"Id\"", orderBy, StringComparison.Ordinal);
            Assert.True(orderBy.LastIndexOf("\"Id\"", StringComparison.Ordinal)
                        > orderBy.IndexOf("\"Name\"", StringComparison.Ordinal),
                "The primary key must come LAST in the ORDER BY, as the tie-breaker.");
        }

        [Fact]
        public void A_Descending_Sort_Also_Gets_The_Tie_Breaker()
        {
            var sql = SqlFor(new DoctorSpecification(new DoctorSpecParams { Sort = "nameDesc" }));
            var orderBy = sql[sql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];

            Assert.Contains("DESC", orderBy, StringComparison.Ordinal);
            Assert.Contains("\"Id\"", orderBy, StringComparison.Ordinal);
        }

        [Fact]
        public void An_Unsorted_Specification_Is_Still_Ordered()
        {
            // OFFSET/FETCH without ORDER BY is not merely unstable, it is undefined.
            var sql = SqlFor(new DoctorWithCountSpecification(new DoctorSpecParams()));

            Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Paging_Through_A_Non_Unique_Sort_Visits_Every_Row_Exactly_Once()
        {
            // The user-visible consequence: without a total ordering a row can appear on two
            // consecutive pages, or on none, while Count looks perfectly correct.
            await using var context = NewContext();
            var repository = new GenericRepository<Doctor>(context);

            var seen = new List<int>();
            for (var page = 1; page <= 4; page++)
            {
                var spec = new DoctorSpecification(
                    new DoctorSpecParams { Sort = "nameAsc", PageIndex = page, PageSize = 3 });

                seen.AddRange((await repository.ListAsync(spec)).Select(d => d.Id));
            }

            Assert.Equal(SeededDoctors, seen.Count);
            Assert.Equal(SeededDoctors, seen.Distinct().Count());
        }

        #endregion

        #region Ordering is no longer self-contradictory

        [Fact]
        public void Setting_A_Descending_Sort_Clears_The_Ascending_One()
        {
            // Both set at once used to mean OrderBy ran and was then thrown away by
            // OrderByDescending - a silently discarded sort key.
            var spec = new OrderableSpec();
            spec.SortAscendingByName();
            spec.SortDescendingByName();

            Assert.Null(spec.OrderBy);
            Assert.NotNull(spec.OrderByDescending);
        }

        [Fact]
        public void Setting_An_Ascending_Sort_Clears_The_Descending_One()
        {
            var spec = new OrderableSpec();
            spec.SortDescendingByName();
            spec.SortAscendingByName();

            Assert.NotNull(spec.OrderBy);
            Assert.Null(spec.OrderByDescending);
        }

        [Fact]
        public void A_Contradictory_Specification_Cannot_Reach_The_Query()
        {
            // Even if both setters are called, exactly one sort key survives - so the SQL carries
            // one DESC (the chosen key) and the ascending tie-breaker, never a discarded key.
            var spec = new OrderableSpec();
            spec.SortAscendingByName();
            spec.SortDescendingByName();

            var sql = SqlFor(spec);
            var orderBy = sql[sql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];

            Assert.Equal(1, orderBy.Split("DESC", StringSplitOptions.None).Length - 1);
            Assert.Contains("\"Id\"", orderBy, StringComparison.Ordinal);
        }

        #endregion

        #region Composition order

        [Fact]
        public void Filtering_Happens_Before_Paging()
        {
            var sql = SqlFor(new DoctorSpecification(
                new DoctorSpecParams { Specialty = "Cardiology", PageIndex = 2, PageSize = 3 }));

            var whereIndex = sql.IndexOf("WHERE", StringComparison.Ordinal);
            var limitIndex = sql.IndexOf("LIMIT", StringComparison.Ordinal);

            Assert.True(whereIndex >= 0 && limitIndex > whereIndex,
                "The filter must be applied before the page is taken, or the page is sliced from " +
                "the unfiltered set.");
        }

        [Fact]
        public async Task Includes_Survive_Being_Composed_Before_Paging()
        {
            // ScheduleSpecification both includes the doctor and pages. Includes used to be attached
            // after Skip/Take; they are now attached before, and must still load.
            await using var context = NewContext();
            var doctor = await context.Doctors.FirstAsync();

            for (var i = 0; i < 6; i++)
                context.DoctorSchedules.Add(new DoctorSchedule
                {
                    TenantId = Tenant.DefaultTenantId, DoctorId = doctor.Id, DayOfWeek = (WeekDay)(i % 7),
                    StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17)
                });
            await context.SaveChangesAsync();

            var schedules = await new GenericRepository<DoctorSchedule>(context)
                .ListAsync(new ScheduleSpecification(new DoctorScheduleSpecParams { PageSize = 4 }));

            Assert.Equal(4, schedules.Count);
            Assert.All(schedules, s => Assert.NotNull(s.Doctor));
        }

        [Fact]
        public async Task A_Page_Of_A_Filtered_Set_Contains_Only_Matching_Rows()
        {
            await using var context = NewContext();
            context.Doctors.Add(new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Other", Specialization = "Neurology" });
            await context.SaveChangesAsync();

            var page = await new GenericRepository<Doctor>(context).ListAsync(
                new DoctorSpecification(new DoctorSpecParams { Specialty = "Neurology", PageSize = 5 }));

            Assert.Single(page);
            Assert.Equal("Neurology", page[0].Specialization);
        }

        #endregion

        #region Split query

        [Fact]
        public void Split_Query_Is_Off_Unless_A_Specification_Asks_For_It()
        {
            Assert.False(new DoctorSpecification(new DoctorSpecParams()).AsSplitQuery);
        }

        [Fact]
        public void A_Specification_Can_Request_A_Split_Query()
        {
            var spec = new OrderableSpec();
            spec.Split();

            Assert.True(spec.AsSplitQuery);
        }

        #endregion

        /// <summary>Exposes the protected ordering hooks so they can be exercised directly.</summary>
        private sealed class OrderableSpec : BaseSpecification<Doctor>
        {
            public void SortAscendingByName() => AddOrderBy(d => d.Name);
            public void SortDescendingByName() => AddOrderByDescending(d => d.Name);
            public void Split() => UseSplitQuery();
        }

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
