using Clinic.Domain.Entites;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Repositores;
using Clinic.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Tests for TODO #21 (finding H18), against a real EF Core provider.
    ///
    /// BaseEntity carried only an Id, so Update() issued a blind UPDATE of every column with no
    /// WHERE clause beyond the key. Two users editing the same record both succeeded and the second
    /// silently overwrote the first - a lost update, invisible to everyone involved.
    /// </summary>
    public sealed class ConcurrencyAndAuditTests : IAsyncLifetime
    {
        private static readonly DateTimeOffset CreationTime = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

        private SqliteConnection _connection = default!;
        private DbContextOptions<ClinicDbContext> _options = default!;
        private FakeTimeProvider _clock = default!;
        private StubCurrentUser _user = default!;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;

            _clock = new FakeTimeProvider(CreationTime);
            _user = new StubCurrentUser("receptionist-1");

            await using var context = NewContext();
            await context.Database.EnsureCreatedAsync();
        }

        private ClinicDbContext NewContext() => new(_options, _user, _clock);

        private async Task<int> SeedDoctorAsync()
        {
            await using var context = NewContext();
            var doctor = new Doctor { Name = "Dr. Aya", Specialization = "Cardiology" };
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();
            return doctor.Id;
        }

        #region Optimistic concurrency

        [Fact]
        public async Task A_Second_Writer_Editing_A_Stale_Copy_Is_Rejected()
        {
            // The lost update, reproduced. Two contexts each load the record, both edit it, both
            // save. Before this item the second save silently won and the first user's change
            // vanished with no error anywhere.
            var doctorId = await SeedDoctorAsync();

            await using var firstUser = NewContext();
            await using var secondUser = NewContext();

            var firstCopy = await firstUser.Doctors.SingleAsync(d => d.Id == doctorId);
            var secondCopy = await secondUser.Doctors.SingleAsync(d => d.Id == doctorId);

            firstCopy.Specialization = "Neurology";
            await firstUser.SaveChangesAsync();

            secondCopy.Specialization = "Dermatology";

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondUser.SaveChangesAsync());
        }

        [Fact]
        public async Task The_First_Writers_Change_Survives()
        {
            // Rejecting the second write is only useful if the first one is actually preserved.
            var doctorId = await SeedDoctorAsync();

            await using (var firstUser = NewContext())
            await using (var secondUser = NewContext())
            {
                var firstCopy = await firstUser.Doctors.SingleAsync(d => d.Id == doctorId);
                var secondCopy = await secondUser.Doctors.SingleAsync(d => d.Id == doctorId);

                firstCopy.Specialization = "Neurology";
                await firstUser.SaveChangesAsync();

                secondCopy.Specialization = "Dermatology";
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondUser.SaveChangesAsync());
            }

            await using var verification = NewContext();
            Assert.Equal("Neurology", (await verification.Doctors.SingleAsync()).Specialization);
        }

        [Fact]
        public async Task Reloading_After_A_Conflict_Lets_The_Second_Writer_Succeed()
        {
            // The recovery path a client is expected to take after a 409.
            var doctorId = await SeedDoctorAsync();

            await using (var firstUser = NewContext())
            {
                var copy = await firstUser.Doctors.SingleAsync(d => d.Id == doctorId);
                copy.Specialization = "Neurology";
                await firstUser.SaveChangesAsync();
            }

            await using var secondUser = NewContext();
            var reloaded = await secondUser.Doctors.SingleAsync(d => d.Id == doctorId);
            reloaded.Specialization = "Dermatology";
            await secondUser.SaveChangesAsync();

            await using var verification = NewContext();
            Assert.Equal("Dermatology", (await verification.Doctors.SingleAsync()).Specialization);
        }

        [Fact]
        public async Task Sequential_Edits_Are_Unaffected()
        {
            // Concurrency control must not break the ordinary case of one user editing twice.
            var doctorId = await SeedDoctorAsync();

            for (var i = 0; i < 3; i++)
            {
                await using var context = NewContext();
                var doctor = await context.Doctors.SingleAsync(d => d.Id == doctorId);
                doctor.Specialization = $"Specialty {i}";
                await context.SaveChangesAsync();
            }

            await using var verification = NewContext();
            Assert.Equal("Specialty 2", (await verification.Doctors.SingleAsync()).Specialization);
        }

        [Fact]
        public async Task The_Token_Changes_On_Every_Update()
        {
            var doctorId = await SeedDoctorAsync();

            Guid afterInsert, afterUpdate;

            await using (var context = NewContext())
            {
                afterInsert = (await context.Doctors.SingleAsync(d => d.Id == doctorId)).RowVersion;
                Assert.NotEqual(Guid.Empty, afterInsert);
            }

            await using (var context = NewContext())
            {
                var doctor = await context.Doctors.SingleAsync(d => d.Id == doctorId);
                doctor.Specialization = "Neurology";
                await context.SaveChangesAsync();
                afterUpdate = doctor.RowVersion;
            }

            Assert.NotEqual(afterInsert, afterUpdate);
        }

        [Fact]
        public async Task Deleting_A_Stale_Copy_Is_Also_Rejected()
        {
            // A delete carries the same risk: removing a record someone else just changed.
            var doctorId = await SeedDoctorAsync();

            await using var firstUser = NewContext();
            await using var secondUser = NewContext();

            var firstCopy = await firstUser.Doctors.SingleAsync(d => d.Id == doctorId);
            var secondCopy = await secondUser.Doctors.SingleAsync(d => d.Id == doctorId);

            firstCopy.Specialization = "Neurology";
            await firstUser.SaveChangesAsync();

            secondUser.Doctors.Remove(secondCopy);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondUser.SaveChangesAsync());
        }

        #endregion

        #region Audit columns

        [Fact]
        public async Task Creation_Records_When_And_By_Whom()
        {
            await SeedDoctorAsync();

            await using var context = NewContext();
            var doctor = await context.Doctors.SingleAsync();

            Assert.Equal(CreationTime, doctor.CreatedAtUtc);
            Assert.Equal("receptionist-1", doctor.CreatedBy);
            Assert.Null(doctor.ModifiedAtUtc);
            Assert.Null(doctor.ModifiedBy);
        }

        [Fact]
        public async Task Modification_Records_When_And_By_Whom()
        {
            var doctorId = await SeedDoctorAsync();

            _clock.Advance(TimeSpan.FromHours(5));
            _user.UserId = "doctor-2";

            await using (var context = NewContext())
            {
                var doctor = await context.Doctors.SingleAsync(d => d.Id == doctorId);
                doctor.Specialization = "Neurology";
                await context.SaveChangesAsync();
            }

            await using var verification = NewContext();
            var updated = await verification.Doctors.SingleAsync();

            Assert.Equal(CreationTime.AddHours(5), updated.ModifiedAtUtc);
            Assert.Equal("doctor-2", updated.ModifiedBy);
        }

        [Fact]
        public async Task An_Update_Cannot_Rewrite_Who_Created_The_Record()
        {
            // Update() marks every property modified. Without protection, a second user's edit would
            // quietly reassign authorship of the original creation - which defeats the point of
            // having an audit column at all.
            var doctorId = await SeedDoctorAsync();

            _user.UserId = "someone-else";
            _clock.Advance(TimeSpan.FromDays(1));

            await using (var context = NewContext())
            {
                var doctor = await context.Doctors.SingleAsync(d => d.Id == doctorId);
                doctor.CreatedBy = "forged";
                doctor.CreatedAtUtc = DateTimeOffset.MaxValue;
                doctor.Specialization = "Neurology";
                await context.SaveChangesAsync();
            }

            await using var verification = NewContext();
            var stored = await verification.Doctors.SingleAsync();

            Assert.Equal("receptionist-1", stored.CreatedBy);
            Assert.Equal(CreationTime, stored.CreatedAtUtc);
        }

        [Fact]
        public async Task Unauthenticated_Work_Records_No_Actor_Rather_Than_A_Wrong_One()
        {
            _user.UserId = null;

            await SeedDoctorAsync();

            await using var context = NewContext();
            var doctor = await context.Doctors.SingleAsync();

            Assert.Null(doctor.CreatedBy);
            Assert.Equal(CreationTime, doctor.CreatedAtUtc);
        }

        [Fact]
        public async Task Audit_Columns_Are_Applied_To_Every_Entity_Not_Just_Doctors()
        {
            // The stamping is in the context, so a new entity type cannot be added without it.
            await using (var context = NewContext())
            {
                context.Patients.Add(new Patient
                {
                    Name = "Sara", Phone = "0100", Gender = "Female",
                    DateOfBirth = new DateTime(1995, 4, 12)
                });
                await context.SaveChangesAsync();
            }

            await using var verification = NewContext();
            var patient = await verification.Patients.SingleAsync();

            Assert.Equal(CreationTime, patient.CreatedAtUtc);
            Assert.Equal("receptionist-1", patient.CreatedBy);
            Assert.NotEqual(Guid.Empty, patient.RowVersion);
        }

        #endregion

        #region Model configuration

        [Fact]
        public void Every_Entity_Carries_A_Concurrency_Token()
        {
            using var context = NewContext();

            foreach (var entityType in context.Model.GetEntityTypes()
                         .Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType)))
            {
                var rowVersion = entityType.FindProperty(nameof(BaseEntity.RowVersion));

                Assert.True(rowVersion is not null, $"{entityType.ClrType.Name} has no RowVersion.");
                Assert.True(rowVersion!.IsConcurrencyToken,
                    $"{entityType.ClrType.Name}.RowVersion is not marked as a concurrency token, so " +
                    "updates to it will not be checked.");
            }
        }

        [Fact]
        public async Task The_Repository_Path_Is_Protected_Too()
        {
            // The application writes through GenericRepository and UnitOfWork, not the context
            // directly, so the protection has to hold on that path.
            var doctorId = await SeedDoctorAsync();

            await using var firstUser = NewContext();
            await using var secondUser = NewContext();

            var first = await new GenericRepository<Doctor>(firstUser).GetByIdAsync(doctorId);
            var second = await new GenericRepository<Doctor>(secondUser).GetByIdAsync(doctorId);

            first.Specialization = "Neurology";
            await new UnitOfWork(firstUser).CompleteAsync();

            second.Specialization = "Dermatology";

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => new UnitOfWork(secondUser).CompleteAsync());
        }

        #endregion

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
