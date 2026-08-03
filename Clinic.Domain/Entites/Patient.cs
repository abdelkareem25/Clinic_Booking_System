namespace Clinic.Domain.Entites
{
    public class Patient :BaseEntity
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }

        /// <summary>
        /// The AspNetUsers account for this person, when they have one.
        ///
        /// Nullable on purpose: staff routinely create a patient record before - or without - the
        /// patient ever having a login. This is the link that makes an ownership check expressible
        /// ("is this record mine?"), which was impossible while identity lived in a separate
        /// database. See TODO #23; enforcing it is a follow-on.
        /// </summary>
        public string? UserId { get; set; }

        public ICollection<Appointment> Appointments { get; set; } = new HashSet<Appointment>();
    }
}
