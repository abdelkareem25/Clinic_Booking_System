using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.PatientDto
{
    public class GetPatientDto
    {

        [Required]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Phone { get; set; }
        [Required]
        public DateTime DateOfBirth { get; set; }
        [Required]
        public string Gender { get; set; }
    }
}
