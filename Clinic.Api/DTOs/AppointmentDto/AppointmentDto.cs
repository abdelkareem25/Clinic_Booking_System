namespace Clinic.Api.DTOs.AppointmentDto
{
    public class AppointmentDto
    {
        public int Id { get; set; }
        public string DoctorName { get; set; }
        public string PatientName { get; set; }
        public DateTime AppointmentDate { get; set; }
    }
}
