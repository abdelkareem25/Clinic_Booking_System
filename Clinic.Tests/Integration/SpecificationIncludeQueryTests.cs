using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.AppointmentSpec;
using Clinic.Domain.Interfaces.Specifications.PatientSpec;
using Clinic.Domain.Interfaces.Specifications.ScheduleSpec;
using Clinic.Infrastructure.Data.Context;
using Clinic.Tests.TestSupport;
using Clinic.Infrastructure.Repositores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Integration tests for TODO #5 (finding C5).
    ///
    /// The shape guard in SpecificationIncludeTests reasons about expression trees; this suite runs
    /// the specifications through the real SpecificationEvaluator against a real EF Core provider.
    /// An invalid Include throws InvalidOperationException here, exactly as it did in production,
    /// and the assertions on the loaded navigations prove the includes do their job rather than
    /// merely being accepted.
    /// </summary>
    public sealed class SpecificationIncludeQueryTests : IAsyncLifetime
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

            var doctor = new Doctor { TenantId = Tenant.DefaultTenantId, Name = "Dr. Aya", Specialization = "Cardiology" };
            var patient = new Patient
            {
                TenantId = Tenant.DefaultTenantId, Name = "Sara",
                Phone = "01000000000",
                Gender = "Female",
                DateOfBirth = new DateTime(1995, 4, 12)
            };
            context.Doctors.Add(doctor);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            context.Appointments.Add(new Appointment
            {
                TenantId = Tenant.DefaultTenantId, DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentDate = new DateTime(2026, 8, 3, 10, 0, 0)
            });
            context.DoctorSchedules.Add(new DoctorSchedule
            {
                TenantId = Tenant.DefaultTenantId, DoctorId = doctor.Id,
                DayOfWeek = WeekDay.Wednesday,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0)
            });
            await context.SaveChangesAsync();
        }

        private ClinicDbContext NewContext() => new(_options, currentTenant: new StubCurrentTenant());

        [Fact]
        public async Task AppointmentWithDoctorAndPatientSpec_Executes_And_Loads_Both_Navigations()
        {
            // This is the query behind GET /api/Appointments/{id}, which threw
            // "The expression 'a => a.Doctor.Name' is invalid inside an 'Include' operation".
            await using var context = NewContext();
            var repository = new GenericRepository<Appointment>(context);

            var appointment = await repository.GetEntityWithSpec(new AppointmentWithDoctorAndPatientSpec(1));

            Assert.NotNull(appointment);
            Assert.NotNull(appointment!.Doctor);
            Assert.NotNull(appointment.Patient);
            Assert.Equal("Dr. Aya", appointment.Doctor.Name);
            Assert.Equal("Sara", appointment.Patient.Name);
        }

        [Fact]
        public async Task AppointmentWithDoctorAndPatientSpec_Parameterless_Executes()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<Appointment>(context);

            var appointments = await repository.ListAsync(new AppointmentWithDoctorAndPatientSpec());

            Assert.Single(appointments);
            Assert.NotNull(appointments[0].Doctor);
            Assert.NotNull(appointments[0].Patient);
        }

        [Fact]
        public async Task AppointmentWithDoctorNameSpec_Executes_And_Loads_Both_Navigations()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<Appointment>(context);

            var appointments = await repository.ListAsync(new AppointmentWithDoctorNameSpec("Dr. Aya"));

            Assert.Single(appointments);
            Assert.Equal("Dr. Aya", appointments[0].Doctor.Name);
            Assert.Equal("Sara", appointments[0].Patient.Name);   // needed by AppointmentDto.PatientName
        }

        [Fact]
        public async Task AppointmentWithDoctorNameSpec_Parameterless_Executes()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<Appointment>(context);

            var appointments = await repository.ListAsync(new AppointmentWithDoctorNameSpec());

            Assert.Single(appointments);
            Assert.NotNull(appointments[0].Doctor);
        }

        [Fact]
        public async Task AppointmentWithPatientNameSpec_Executes_And_Loads_Both_Navigations()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<Appointment>(context);

            var appointments = await repository.ListAsync(new AppointmentWithPatientNameSpec("Sara"));

            Assert.Single(appointments);
            Assert.Equal("Sara", appointments[0].Patient.Name);
            Assert.Equal("Dr. Aya", appointments[0].Doctor.Name);
        }

        [Fact]
        public async Task AppointmentWithPatientNameSpec_Parameterless_Executes()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<Appointment>(context);

            var appointments = await repository.ListAsync(new AppointmentWithPatientNameSpec());

            Assert.Single(appointments);
            Assert.NotNull(appointments[0].Patient);
            Assert.NotNull(appointments[0].Doctor);
        }

        [Fact]
        public async Task ScheduleSpecification_Executes_And_Loads_The_Doctor()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<DoctorSchedule>(context);

            var schedules = await repository.ListAsync(new ScheduleSpecification(new DoctorScheduleSpecParams()));

            Assert.Single(schedules);
            Assert.NotNull(schedules[0].Doctor);
            Assert.Equal("Dr. Aya", schedules[0].Doctor.Name);
        }

        [Fact]
        public async Task PatientsWithAppointmentsSpecification_Executes_And_Loads_The_Collection()
        {
            await using var context = NewContext();
            var repository = new GenericRepository<Patient>(context);

            var patients = await repository.ListAsync(new PatientsWithAppointmentsSpecification());

            Assert.Single(patients);
            Assert.Single(patients[0].Appointments);
        }

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
