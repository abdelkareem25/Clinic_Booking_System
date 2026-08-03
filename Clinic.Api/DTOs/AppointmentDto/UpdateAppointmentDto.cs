using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.AppointmentDto
{
    public class UpdateAppointmentDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId is required.")]
        public int DoctorId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "PatientId is required.")]
        public int PatientId { get; set; }

        [Required]
        public DateTime AppointmentDate { get; set; }

        [Range(AppointmentDurations.MinimumMinutes, AppointmentDurations.MaximumMinutes)]
        public int DurationMinutes { get; set; } = AppointmentDurations.DefaultMinutes;
    }
}
