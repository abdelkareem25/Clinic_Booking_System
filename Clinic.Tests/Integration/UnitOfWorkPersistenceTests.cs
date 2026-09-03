using Clinic.Domain.Entites;
using Clinic.Infrastructure.Data.Context;
using Clinic.Tests.TestSupport;
using Clinic.Infrastructure.Repositores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Integration tests for TODO #1 (finding C1), exercising the real ClinicDbContext,
    /// GenericRepository and UnitOfWork against an in-memory SQLite database.
    ///
    /// Every assertion reads back through a *fresh* DbContext. That is the whole point: reading
    /// through the same context would be satisfied by the change tracker and would still pass
    /// against the broken code. Only a separate context proves the row reached the database.
    /// </summary>
    public sealed class UnitOfWorkPersistenceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ClinicDbContext> _options;

        public UnitOfWorkPersistenceTests()
        {
            // A shared open connection keeps the in-memory database alive across contexts.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var schema = new ClinicDbContext(_options, currentTenant: new StubCurrentTenant());
            schema.Database.EnsureCreated();
        }

        private ClinicDbContext NewContext() => new(_options, currentTenant: new StubCurrentTenant());

        [Fact]
        public async Task Add_Without_Complete_Does_Not_Persist()
        {
            // Documents the exact defect C1 described: the repository alone only stages the change.
            await using (var context = NewContext())
            {
                var repository = new GenericRepository<Doctor>(context);
                await repository.AddAsync(new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Ghost", Specialization = "Radiology" });
                // No CompleteAsync() -> the change tracker is discarded with the context.
            }

            await using var verification = NewContext();
            Assert.Empty(await verification.Doctors.ToListAsync());
        }

        [Fact]
        public async Task Add_Then_Complete_Persists_And_Assigns_Identity()
        {
            int generatedId;

            await using (var context = NewContext())
            {
                var unitOfWork = new UnitOfWork(context);
                var doctor = new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Aya", Specialization = "Cardiology" };

                await unitOfWork.Repository<Doctor>().AddAsync(doctor);
                Assert.Equal(0, doctor.Id); // no key before the commit

                var affected = await unitOfWork.CompleteAsync();

                Assert.Equal(1, affected);
                Assert.NotEqual(0, doctor.Id); // SaveChangesAsync populated the identity value
                generatedId = doctor.Id;
            }

            await using var verification = NewContext();
            var persisted = await verification.Doctors.SingleAsync();
            Assert.Equal(generatedId, persisted.Id);
            Assert.Equal("Dr. Aya", persisted.Name);
            Assert.Equal("Cardiology", persisted.Specialization);
        }

        [Fact]
        public async Task Update_Then_Complete_Persists()
        {
            await using (var seed = NewContext())
            {
                seed.Doctors.Add(new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Aya", Specialization = "Cardiology" });
                await seed.SaveChangesAsync();
            }

            await using (var context = NewContext())
            {
                var unitOfWork = new UnitOfWork(context);
                var repository = unitOfWork.Repository<Doctor>();

                var doctor = await repository.GetByIdAsync(1);
                doctor.Specialization = "Neurology";

                await repository.UpdateAsync(doctor);
                await unitOfWork.CompleteAsync();
            }

            await using var verification = NewContext();
            Assert.Equal("Neurology", (await verification.Doctors.SingleAsync()).Specialization);
        }

        [Fact]
        public async Task Delete_Then_Complete_Persists()
        {
            await using (var seed = NewContext())
            {
                seed.Doctors.Add(new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Aya", Specialization = "Cardiology" });
                await seed.SaveChangesAsync();
            }

            await using (var context = NewContext())
            {
                var unitOfWork = new UnitOfWork(context);
                var repository = unitOfWork.Repository<Doctor>();

                await repository.DeleteAsync(await repository.GetByIdAsync(1));
                await unitOfWork.CompleteAsync();
            }

            await using var verification = NewContext();
            Assert.Empty(await verification.Doctors.ToListAsync());
        }

        [Fact]
        public async Task Complete_Commits_Every_Repository_In_One_Transaction()
        {
            // The point of a unit of work: writes staged through different repositories
            // are flushed together by a single CompleteAsync().
            await using (var context = NewContext())
            {
                var unitOfWork = new UnitOfWork(context);

                var doctor = new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Aya", Specialization = "Cardiology" };
                var patient = new Patient
                {
                    TenantId = Tenant.DefaultTenantId, Name = "Sara",
                    Phone = "01000000000",
                    Gender = "Female",
                    DateOfBirth = new DateTime(1995, 4, 12)
                };

                await unitOfWork.Repository<Doctor>().AddAsync(doctor);
                await unitOfWork.Repository<Patient>().AddAsync(patient);

                var affected = await unitOfWork.CompleteAsync();
                Assert.Equal(2, affected); // one round trip, both rows
            }

            await using var verification = NewContext();
            Assert.Single(await verification.Doctors.ToListAsync());
            Assert.Single(await verification.Patients.ToListAsync());
        }

        [Fact]
        public async Task Appointment_Written_Through_AppointmentRepository_Is_Committed_By_UnitOfWork()
        {
            // AppointmentsController uses IAppointmentRepository directly but commits via IUnitOfWork.
            // That only works because both resolve the same scoped ClinicDbContext.
            await using (var context = NewContext())
            {
                var unitOfWork = new UnitOfWork(context);
                var appointments = new AppointmentRepository(context);

                var doctor = new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Aya", Specialization = "Cardiology" };
                var patient = new Patient
                {
                    TenantId = Tenant.DefaultTenantId, Name = "Sara",
                    Phone = "01000000000",
                    Gender = "Female",
                    DateOfBirth = new DateTime(1995, 4, 12)
                };
                await unitOfWork.Repository<Doctor>().AddAsync(doctor);
                await unitOfWork.Repository<Patient>().AddAsync(patient);
                await unitOfWork.CompleteAsync();

                await appointments.AddAsync(new Appointment
                {
                    TenantId = Tenant.DefaultTenantId, DoctorId = doctor.Id,
                    PatientId = patient.Id,
                    AppointmentDate = new DateTime(2026, 8, 3, 10, 0, 0)
                });

                // Committed through the unit of work, not the appointment repository.
                Assert.Equal(1, await unitOfWork.CompleteAsync());
            }

            await using var verification = NewContext();
            Assert.Single(await verification.Appointments.ToListAsync());
        }

        public void Dispose() => _connection.Dispose();
    }
}
