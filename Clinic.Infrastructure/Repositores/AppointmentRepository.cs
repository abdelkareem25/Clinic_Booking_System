using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Specifications;
using Clinic.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Repositores
{
    public class AppointmentRepository : GenericRepository<Appointment> , IAppointmentRepository
    {
        private readonly ClinicDbContext _context;

        public AppointmentRepository(ClinicDbContext context) : base(context)
        {
            _context = context;
        }
        public async Task<IReadOnlyList<Appointment>> GetAppointmentsByDoctorIdAsync(int doctorId)
        {
            return await _context.Appointments.Where(a => a.DoctorId == doctorId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Appointment>> GetAppointmentsByPatientIdAsync(int patientId)
        {
            return await _context.Appointments.Where(a=>a.PatientId == patientId)
                .ToListAsync();
        }

        public Task<bool> HasOverlappingAppointmentAsync(
            int doctorId, DateTime startsAt, DateTime endsAt, int? excludeAppointmentId)
        {
            // Half-open intervals [start, end): two appointments overlap when each starts before the
            // other ends. Appointments that merely touch - one ending exactly as the next begins -
            // do NOT overlap, which is what keeps back-to-back slots bookable.
            return _context.Appointments
                .AsNoTracking()
                .AnyAsync(a => a.DoctorId == doctorId
                            && (excludeAppointmentId == null || a.Id != excludeAppointmentId.Value)
                            && a.AppointmentDate < endsAt
                            && startsAt < a.EndTime);
        }

        public async Task<bool> IsWithinWorkingHoursAsync(int doctorId, DateTime startsAt, DateTime endsAt)
        {
            // A schedule block is a day of the week plus a time range, so an appointment running
            // past midnight cannot sit inside one.
            if (startsAt.Date != endsAt.Date) return false;

            var dayOfWeek = (WeekDay)(int)startsAt.DayOfWeek;

            // Only the day filter runs in SQL. TimeSpan comparison is not translatable on every
            // provider - SQLite stores it as text and refuses the query outright - and a doctor has
            // at most a handful of blocks on any given weekday, so the containment check is done in
            // memory over a tiny, index-served result set.
            var blocks = await _context.DoctorSchedules
                .AsNoTracking()
                .Where(s => s.DoctorId == doctorId && s.DayOfWeek == dayOfWeek)
                .Select(s => new { s.StartTime, s.EndTime })
                .ToListAsync();

            var startTime = startsAt.TimeOfDay;
            var endTime = endsAt.TimeOfDay;

            return blocks.Any(block => block.StartTime <= startTime && endTime <= block.EndTime);
        }
    }
}
