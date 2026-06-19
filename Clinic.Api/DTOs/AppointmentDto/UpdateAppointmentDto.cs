namespace Clinic.Api.DTOs.AppointmentDto
{
    public class UpdateAppointmentDto
    {
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public DateTime AppointmentDate { get; set; }
    }
}
