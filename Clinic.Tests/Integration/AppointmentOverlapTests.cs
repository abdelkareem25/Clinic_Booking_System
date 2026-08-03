using Clinic.Domain.Entites;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Repositores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Tests for TODO #17 (finding H2), covering the availability rules against a real database.
    ///
    /// The old check was `a.AppointmentDate == appointmentDate`: two appointments one minute apart
    /// were both "available", the StartTime/EndTime columns describing the real interval were never
    /// read, and DoctorSchedule was consulted by nothing at all.
    /// </summary>
    public sealed class AppointmentOverlapTests : IAsyncLifetime
    {
        // A Monday.
        private static readonly DateTime Monday = new(2026, 8, 3);

        private SqliteConnection _connection = default!;
        private DbContextOptions<ClinicDbContext> _options = default!;
        private int _doctorId;
        private int _otherDoctorId;
        private int _patientId;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;

            await using var context = NewContext();
            await context.Database.EnsureCreatedAsync();

            var doctor = new Doctor { Name = "Dr. Aya", Specialization = "Cardiology" };
            var otherDoctor = new Doctor { Name = "Dr. Omar", Specialization = "Neurology" };
            var patient = new Patient
            {
                Name = "Sara", Phone = "01000000000", Gender = "Female",
                DateOfBirth = new DateTime(1995, 4, 12)
            };
            context.Doctors.AddRange(doctor, otherDoctor);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            _doctorId = doctor.Id;
            _otherDoctorId = otherDoctor.Id;
            _patientId = patient.Id;

            // Both doctors work Mondays, 09:00-17:00.
            context.DoctorSchedules.AddRange(
                new DoctorSchedule
                {
                    DoctorId = _doctorId, DayOfWeek = WeekDay.Monday,
                    StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0)
                },
                new DoctorSchedule
                {
                    DoctorId = _otherDoctorId, DayOfWeek = WeekDay.Monday,
                    StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0)
                });
            await context.SaveChangesAsync();
        }

        private ClinicDbContext NewContext() => new(_options);

        private async Task BookAsync(int doctorId, DateTime startsAt, int minutes = 30)
        {
            await using var context = NewContext();
            context.Appointments.Add(new Appointment
            {
                DoctorId = doctorId,
                PatientId = _patientId,
                AppointmentDate = startsAt,
                StartTime = startsAt,
                EndTime = startsAt.AddMinutes(minutes)
            });
            await context.SaveChangesAsync();
        }

        private async Task<bool> OverlapsAsync(DateTime startsAt, int minutes = 30, int? excludeId = null)
        {
            await using var context = NewContext();
            return await new AppointmentRepository(context).HasOverlappingAppointmentAsync(
                _doctorId, startsAt, startsAt.AddMinutes(minutes), excludeId);
        }

        private async Task<bool> WithinHoursAsync(DateTime startsAt, int minutes = 30)
        {
            await using var context = NewContext();
            return await new AppointmentRepository(context).IsWithinWorkingHoursAsync(
                _doctorId, startsAt, startsAt.AddMinutes(minutes));
        }

        #region Overlap detection

        [Fact]
        public async Task An_Identical_Slot_Overlaps()
        {
            await BookAsync(_doctorId, Monday.AddHours(10));

            Assert.True(await OverlapsAsync(Monday.AddHours(10)));
        }

        [Fact]
        public async Task A_Slot_Starting_One_Minute_Later_Overlaps()
        {
            // The exact case the old exact-equality check waved through.
            await BookAsync(_doctorId, Monday.AddHours(10));

            Assert.True(await OverlapsAsync(Monday.AddHours(10).AddMinutes(1)));
        }

        [Fact]
        public async Task A_Slot_Starting_Before_And_Running_Into_An_Existing_One_Overlaps()
        {
            await BookAsync(_doctorId, Monday.AddHours(10));

            Assert.True(await OverlapsAsync(Monday.AddHours(9).AddMinutes(45)));
        }

        [Fact]
        public async Task A_Slot_Entirely_Containing_An_Existing_One_Overlaps()
        {
            await BookAsync(_doctorId, Monday.AddHours(10), minutes: 15);

            Assert.True(await OverlapsAsync(Monday.AddHours(9), minutes: 120));
        }

        [Fact]
        public async Task Back_To_Back_Slots_Do_Not_Overlap()
        {
            // Half-open intervals: 10:00-10:30 and 10:30-11:00 touch but do not collide. Getting
            // this wrong would make a full day of consecutive appointments unbookable.
            await BookAsync(_doctorId, Monday.AddHours(10));

            Assert.False(await OverlapsAsync(Monday.AddHours(10).AddMinutes(30)));
        }

        [Fact]
        public async Task A_Slot_Ending_Exactly_When_An_Existing_One_Starts_Does_Not_Overlap()
        {
            await BookAsync(_doctorId, Monday.AddHours(10));

            Assert.False(await OverlapsAsync(Monday.AddHours(9).AddMinutes(30)));
        }

        [Fact]
        public async Task Another_Doctors_Appointment_Is_Irrelevant()
        {
            await BookAsync(_otherDoctorId, Monday.AddHours(10));

            Assert.False(await OverlapsAsync(Monday.AddHours(10)));
        }

        [Fact]
        public async Task An_Appointment_Does_Not_Collide_With_Itself_When_Excluded()
        {
            // Rescheduling: the row being moved must not be treated as a conflict.
            await BookAsync(_doctorId, Monday.AddHours(10));

            await using var context = NewContext();
            var existingId = (await context.Appointments.SingleAsync()).Id;

            Assert.True(await OverlapsAsync(Monday.AddHours(10)));
            Assert.False(await OverlapsAsync(Monday.AddHours(10), excludeId: existingId));
        }

        [Fact]
        public async Task A_Longer_Appointment_Blocks_More_Of_The_Day()
        {
            // Duration is now load-bearing; it was not even read before.
            await BookAsync(_doctorId, Monday.AddHours(10), minutes: 120);

            Assert.True(await OverlapsAsync(Monday.AddHours(11), minutes: 30));
            Assert.False(await OverlapsAsync(Monday.AddHours(12), minutes: 30));
        }

        #endregion

        #region Working hours

        [Fact]
        public async Task A_Slot_Inside_The_Published_Hours_Is_Allowed()
        {
            Assert.True(await WithinHoursAsync(Monday.AddHours(10)));
        }

        [Fact]
        public async Task A_Slot_At_Three_In_The_Morning_Is_Refused()
        {
            // The review's example: bookable before, because DoctorSchedule was never consulted.
            Assert.False(await WithinHoursAsync(Monday.AddHours(3)));
        }

        [Fact]
        public async Task A_Slot_On_A_Day_The_Doctor_Does_Not_Work_Is_Refused()
        {
            var tuesday = Monday.AddDays(1);

            Assert.False(await WithinHoursAsync(tuesday.AddHours(10)));
        }

        [Fact]
        public async Task A_Slot_Overrunning_The_End_Of_The_Day_Is_Refused()
        {
            // Starts inside the block but finishes after it: 16:45 + 30 minutes = 17:15.
            Assert.False(await WithinHoursAsync(Monday.AddHours(16).AddMinutes(45)));
        }

        [Fact]
        public async Task A_Slot_Ending_Exactly_At_Closing_Time_Is_Allowed()
        {
            Assert.True(await WithinHoursAsync(Monday.AddHours(16).AddMinutes(30)));
        }

        [Fact]
        public async Task A_Slot_Starting_Exactly_At_Opening_Time_Is_Allowed()
        {
            Assert.True(await WithinHoursAsync(Monday.AddHours(9)));
        }

        [Fact]
        public async Task A_Doctor_With_No_Published_Hours_Cannot_Be_Booked()
        {
            // Deliberate: a doctor who has published no working hours is not bookable. The
            // permissive alternative silently disables the check for every doctor whose schedule
            // someone forgot to enter.
            await using var context = NewContext();
            var unscheduled = new Doctor { Name = "Dr. New", Specialization = "Locum" };
            context.Doctors.Add(unscheduled);
            await context.SaveChangesAsync();

            var repository = new AppointmentRepository(context);

            Assert.False(await repository.IsWithinWorkingHoursAsync(
                unscheduled.Id, Monday.AddHours(10), Monday.AddHours(10).AddMinutes(30)));
        }

        #endregion

        #region Database backstop

        [Fact]
        public async Task The_Database_Refuses_A_Second_Booking_At_The_Same_Instant()
        {
            // The race the application check cannot close on its own: two requests both pass the
            // overlap check, then both insert.
            await BookAsync(_doctorId, Monday.AddHours(10));

            var duplicate = await Assert.ThrowsAnyAsync<DbUpdateException>(
                () => BookAsync(_doctorId, Monday.AddHours(10)));

            Assert.Contains("UNIQUE", duplicate.InnerException?.Message ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task The_Unique_Index_Is_Scoped_To_One_Doctor()
        {
            // Two doctors at the same time is normal clinic operation, not a conflict.
            await BookAsync(_doctorId, Monday.AddHours(10));
            await BookAsync(_otherDoctorId, Monday.AddHours(10));

            await using var context = NewContext();
            Assert.Equal(2, await context.Appointments.CountAsync());
        }

        #endregion

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }
    }
}
