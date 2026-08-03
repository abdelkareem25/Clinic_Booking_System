using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Domain.Interfaces.Specifications;

namespace Clinic.Domain.Interfaces
{
    public interface IAppointmentRepository : IGenericRepository<Appointment>
    {
        /// <summary>
        /// True when the doctor already has an appointment overlapping [startsAt, endsAt).
        ///
        /// Replaces IsDoctorAvailableAsync, which compared AppointmentDate for exact equality. Two
        /// appointments one minute apart were therefore both "available", and the StartTime/EndTime
        /// columns describing the actual interval were never consulted at all.
        /// </summary>
        Task<bool> HasOverlappingAppointmentAsync(
            int doctorId, DateTime startsAt, DateTime endsAt, int? excludeAppointmentId);

        /// <summary>
        /// True when [startsAt, endsAt) falls entirely inside one of the doctor's published schedule
        /// blocks for that day of the week.
        ///
        /// DoctorSchedule existed but was consulted by nothing, so a patient could be booked at
        /// 03:00 on a day the doctor does not work.
        /// </summary>
        Task<bool> IsWithinWorkingHoursAsync(int doctorId, DateTime startsAt, DateTime endsAt);

        Task<IReadOnlyList<Appointment>> GetAppointmentsByPatientIdAsync(int patientId);
        Task<IReadOnlyList<Appointment>> GetAppointmentsByDoctorIdAsync(int doctorId);

    }
}
