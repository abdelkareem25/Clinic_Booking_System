using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.DoctorDto
{
    public class UpdateDoctorDto : IDoctorProfileFields
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Specialization { get; set; }

        public string? Phone { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public decimal? ConsultationFee { get; set; }

        public string? Bio { get; set; }

        public bool IsActive { get; set; } = true;

        // Schedules are deliberately absent. Replacing a doctor's whole rota as a side effect of
        // renaming them would delete shifts that appointments are already booked against; the
        // Schedule endpoints edit shifts one at a time, where that consequence is visible.
    }
}
