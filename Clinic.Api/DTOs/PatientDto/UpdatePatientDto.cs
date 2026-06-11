namespace Clinic.Api.DTOs.PatientDto
{
    public class UpdatePatientDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }
    }
}
