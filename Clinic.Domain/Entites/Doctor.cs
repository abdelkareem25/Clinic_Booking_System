namespace Clinic.Domain.Entites
{
    public class Doctor : BaseEntity
    {
        public string Name { get; set; }
        public string Specialization { get; set; }

        /// <summary>Contact number for the practitioner. Optional - locum cover often has none on file.</summary>
        public string? Phone { get; set; }

        public string? Email { get; set; }

        /// <summary>
        /// Standard fee for one consultation, in the clinic's currency.
        ///
        /// Nullable rather than 0: "we have not recorded a fee" and "this consultation is free"
        /// are different facts, and billing has to be able to tell them apart.
        /// </summary>
        public decimal? ConsultationFee { get; set; }

        /// <summary>Free-text profile shown on the doctor card.</summary>
        public string? Bio { get; set; }

        /// <summary>
        /// Whether the doctor is currently practising.
        ///
        /// A doctor who has left keeps every historical appointment that references them, so the
        /// row cannot simply be deleted. Deactivating removes them from booking without erasing
        /// the record of care they gave.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>The AspNetUsers account for this doctor, when they have one. See Patient.UserId.</summary>
        public string? UserId { get; set; }
        public ICollection<DoctorSchedule> DoctorSchedules { get; set; } = new HashSet<DoctorSchedule>();

        public ICollection<Appointment> Appointments { get; set; } = new HashSet<Appointment>();
    }
}
