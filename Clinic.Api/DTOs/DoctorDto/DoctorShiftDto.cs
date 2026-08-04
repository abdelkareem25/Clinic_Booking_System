using Clinic.Domain.Entites;

namespace Clinic.Api.DTOs.DoctorDto
{
    /// <summary>
    /// One working shift, as supplied alongside a doctor on create.
    ///
    /// Deliberately NOT CreateDoctorScheduleDto: that DTO carries a DoctorId, and here there is no
    /// doctor yet - the id is assigned by the same transaction that writes these rows. Accepting a
    /// DoctorId on this payload would let a caller graft shifts onto a different doctor.
    /// </summary>
    public class DoctorShiftDto
    {
        public WeekDay WeekDay { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }
    }
}
