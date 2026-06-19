using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.DoctorDto
{
    public class DoctorDto
    {
        [Required]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Specialization { get; set; }
    }
}
