using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.DoctorDto
{
    /// <summary>
    /// The shape every doctor response shares.
    ///
    /// <see cref="DoctorDto"/> and <see cref="GetDoctorDto"/> were byte-for-byte identical, so the
    /// new profile fields would have had to be added to both - and the two would drift the first
    /// time only one was updated. Both names are kept because controllers and tests reference them
    /// by name; only the duplicated member list is gone.
    /// </summary>
    public abstract class DoctorResponseBase
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Specialization { get; set; }

        public string? Phone { get; set; }

        public string? Email { get; set; }

        public decimal? ConsultationFee { get; set; }

        public string? Bio { get; set; }

        public bool IsActive { get; set; }
    }
}
