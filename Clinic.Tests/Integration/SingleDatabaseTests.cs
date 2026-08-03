using Clinic.Domain.Entites;
using Clinic.Domain.Entites.Identity;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Repositores;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Tests for TODO #23 (finding H10).
    ///
    /// Identity and clinical records lived in two contexts over two physical databases. These tests
    /// assert the two things that split actually cost: no atomic operation could span both, and no
    /// foreign key could link a patient to their login - which is why any authenticated user can
    /// still read any patient's records.
    /// </summary>
    public sealed class SingleDatabaseTests : IAsyncLifetime
    {
        private SqliteConnection _connection = default!;
        private DbContextOptions<ClinicDbContext> _options = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;

            await using var context = NewContext();
            await context.Database.EnsureCreatedAsync();
        }

        private ClinicDbContext NewContext() => new(_options);

        private IReadOnlyList<string> TableNames()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

            var tables = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));
            return tables;
        }

        #region One database

        [Fact]
        public void Identity_And_Clinical_Tables_Share_One_Schema()
        {
            var tables = TableNames();

            Assert.Contains("Patients", tables);
            Assert.Contains("Doctors", tables);
            Assert.Contains("Appointments", tables);
            Assert.Contains("AspNetUsers", tables);
            Assert.Contains("AspNetRoles", tables);
            Assert.Contains("AspNetUserRoles", tables);
        }

        [Fact]
        public void There_Is_Only_One_DbContext_For_The_Application()
        {
            // ClinicIdentityDbContext used to be a second context over a second connection string.
            var contexts = typeof(ClinicDbContext).Assembly
                .GetTypes()
                .Where(t => typeof(DbContext).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.Name)
                .ToList();

            Assert.Equal(["ClinicDbContext"], contexts);
        }

        [Fact]
        public void The_Context_Maps_Both_Halves_Of_The_Model()
        {
            using var context = NewContext();
            var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType).ToList();

            Assert.Contains(typeof(Patient), entityTypes);
            Assert.Contains(typeof(AppUser), entityTypes);
        }

        #endregion

        #region What the split made impossible: atomicity

        [Fact]
        public async Task An_Account_And_A_Clinical_Record_Commit_Or_Roll_Back_Together()
        {
            // Provisioning a doctor's login and their Doctor record used to be two commits against
            // two databases. If the second failed, the first was already permanent and there was no
            // way to undo it without a distributed transaction.
            await using var context = NewContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            context.Users.Add(new AppUser
            {
                Id = "u-rollback", UserName = "rollback@clinic.local",
                Email = "rollback@clinic.local", DisplayName = "Rollback"
            });
            context.Doctors.Add(new Doctor { Name = "Dr. Rollback", Specialization = "Cardiology" });
            await context.SaveChangesAsync();

            await transaction.RollbackAsync();

            await using var verification = NewContext();
            Assert.Empty(await verification.Users.ToListAsync());
            Assert.Empty(await verification.Doctors.ToListAsync());
        }

        [Fact]
        public async Task A_Committed_Transaction_Persists_Both_Halves()
        {
            await using (var context = NewContext())
            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                context.Users.Add(new AppUser
                {
                    Id = "u-commit", UserName = "commit@clinic.local",
                    Email = "commit@clinic.local", DisplayName = "Commit"
                });
                context.Doctors.Add(new Doctor
                {
                    Name = "Dr. Commit", Specialization = "Cardiology", UserId = "u-commit"
                });
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            await using var verification = NewContext();
            Assert.Single(await verification.Users.ToListAsync());
            Assert.Equal("u-commit", (await verification.Doctors.SingleAsync()).UserId);
        }

        #endregion

        #region What the split made impossible: the ownership link

        [Fact]
        public async Task A_Patient_Record_Can_Point_At_The_Account_For_That_Person()
        {
            await using var context = NewContext();
            context.Users.Add(new AppUser
            {
                Id = "u-sara", UserName = "sara@clinic.local",
                Email = "sara@clinic.local", DisplayName = "Sara"
            });
            context.Patients.Add(new Patient
            {
                Name = "Sara", Phone = "0100", Gender = "Female",
                DateOfBirth = new DateTime(1995, 4, 12), UserId = "u-sara"
            });
            await context.SaveChangesAsync();

            await using var verification = NewContext();
            var patient = await verification.Patients.SingleAsync();

            // This is the comparison an ownership check needs, and it was not expressible before.
            Assert.Equal("u-sara", patient.UserId);
        }

        [Fact]
        public async Task The_Link_Is_A_Real_Foreign_Key()
        {
            // Not merely a string that happens to hold an id: the database refuses a dangling one.
            await using var context = NewContext();
            context.Patients.Add(new Patient
            {
                Name = "Ghost", Phone = "0100", Gender = "Female",
                DateOfBirth = new DateTime(1990, 1, 1), UserId = "no-such-account"
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task Deleting_An_Account_Does_Not_Delete_The_Clinical_Record()
        {
            // Clinical records outlive logins. Cascading the delete would destroy patient history
            // when an account is closed.
            await using (var context = NewContext())
            {
                context.Users.Add(new AppUser
                {
                    Id = "u-leaving", UserName = "leaving@clinic.local",
                    Email = "leaving@clinic.local", DisplayName = "Leaving"
                });
                context.Patients.Add(new Patient
                {
                    Name = "Leaving", Phone = "0100", Gender = "Female",
                    DateOfBirth = new DateTime(1990, 1, 1), UserId = "u-leaving"
                });
                await context.SaveChangesAsync();
            }

            await using (var context = NewContext())
            {
                context.Users.Remove(await context.Users.SingleAsync());
                await context.SaveChangesAsync();
            }

            await using var verification = NewContext();
            var patient = await verification.Patients.SingleAsync();

            Assert.Equal("Leaving", patient.Name);
            Assert.Null(patient.UserId);          // link severed, record intact
        }

        [Fact]
        public async Task A_Clinical_Record_Without_An_Account_Is_Still_Valid()
        {
            // Staff create patient records for people who have never logged in.
            await using var context = NewContext();
            context.Patients.Add(new Patient
            {
                Name = "Walk In", Phone = "0100", Gender = "Male",
                DateOfBirth = new DateTime(1980, 6, 1)
            });
            await context.SaveChangesAsync();

            await using var verification = NewContext();
            Assert.Null((await verification.Patients.SingleAsync()).UserId);
        }

        [Fact]
        public async Task Records_Belonging_To_A_User_Can_Be_Queried_In_One_Round_Trip()
        {
            // Across two databases this needed two queries and a client-side join.
            await using var context = NewContext();
            context.Users.Add(new AppUser
            {
                Id = "u-1", UserName = "one@clinic.local", Email = "one@clinic.local", DisplayName = "One"
            });
            context.Patients.AddRange(
                new Patient { Name = "Mine", Phone = "1", Gender = "F", DateOfBirth = new DateTime(1990, 1, 1), UserId = "u-1" },
                new Patient { Name = "Theirs", Phone = "2", Gender = "F", DateOfBirth = new DateTime(1990, 1, 1) });
            await context.SaveChangesAsync();

            await using var verification = NewContext();
            var mine = await verification.Patients.Where(p => p.UserId == "u-1").ToListAsync();

            Assert.Single(mine);
            Assert.Equal("Mine", mine[0].Name);
        }

        #endregion

        #region Identity still works through the merged context

        [Fact]
        public async Task The_Unit_Of_Work_Sees_Identity_Changes_Too()
        {
            await using var context = NewContext();
            var unitOfWork = new UnitOfWork(context);

            context.Users.Add(new AppUser
            {
                Id = "u-uow", UserName = "uow@clinic.local", Email = "uow@clinic.local", DisplayName = "UoW"
            });
            unitOfWork.Repository<Doctor>();     // same context underneath
            context.Doctors.Add(new Doctor { Name = "Dr. UoW", Specialization = "Cardiology" });

            Assert.Equal(2, await unitOfWork.CompleteAsync());
        }

        [Fact]
        public void Identity_Tables_Keep_Their_Conventional_Names()
        {
            // Merging must not rename them: existing Identity tooling and queries depend on these.
            var tables = TableNames();

            foreach (var expected in new[]
                     {
                         "AspNetUsers", "AspNetRoles", "AspNetUserRoles",
                         "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "AspNetRoleClaims"
                     })
            {
                Assert.Contains(expected, tables);
            }
        }

        #endregion

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
