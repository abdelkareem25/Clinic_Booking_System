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

        public async Task<bool> IsDoctorAvailableAsync(int doctorId, DateTime appointmentDate, int? excludeAppointmentId)
        {
            return !await _context.Appointments.AnyAsync(a => a.DoctorId == doctorId && 
            a.AppointmentDate == appointmentDate && a.Id != excludeAppointmentId);
        }

        
    }
}
