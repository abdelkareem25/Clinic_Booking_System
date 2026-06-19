using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Domain.Interfaces.Specifications;

namespace Clinic.Domain.Interfaces
{
    public interface IAppointmentRepository : IGenericRepository<Appointment>
    {
        Task<bool> IsDoctorAvailableAsync(int doctorId, DateTime appointmentDate, int? excludeAppointmentId);
        Task<IReadOnlyList<Appointment>> GetAppointmentsByPatientIdAsync(int patientId);
        Task<IReadOnlyList<Appointment>> GetAppointmentsByDoctorIdAsync(int doctorId);
        
    }
}
