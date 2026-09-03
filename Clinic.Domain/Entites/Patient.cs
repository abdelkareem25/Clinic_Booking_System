namespace Clinic.Domain.Entites
{
    public class Patient :BaseEntity, ITenantEntity
    {
        // MULTI-TENANT: the clinic that owns this record. Assigned centrally by
        // ClinicDbContext on insert - never by a controller and never from a request
        // payload; see AuditMappingExtensions.IgnoreSystemOwnedMembers.
        public int TenantId { get; set; }

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
