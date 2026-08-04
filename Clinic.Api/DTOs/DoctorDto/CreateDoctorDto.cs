using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.DoctorDto
{
    public class CreateDoctorDto : IDoctorProfileFields
    {
        // Id is deliberately absent. It was [Required] here, which forced every caller to invent a
        // primary key for a row that does not exist yet, while the mapping profile discarded the
        // value anyway (IgnoreSystemOwnedMembers). The database assigns it. Nothing sent one, so
        // removing it breaks no caller - an unknown JSON property is ignored on binding.

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

        /// <summary>
        /// The doctor's working week, written in the same transaction as the doctor itself.
        ///
        /// An empty list means "no published hours yet", which is legitimate - a doctor can be
        /// registered before their rota is agreed. A day the doctor does not work is simply absent;
        /// there is no "off" row to store.
        /// </summary>
        public List<DoctorShiftDto> Schedules { get; set; } = new();
    }
}
