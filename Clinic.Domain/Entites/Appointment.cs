namespace Clinic.Domain.Entites
{
    public class Appointment : BaseEntity, ITenantEntity
    {
        // MULTI-TENANT: the clinic that owns this record. Assigned centrally by
        // ClinicDbContext on insert - never by a controller and never from a request
        // payload; see AuditMappingExtensions.IgnoreSystemOwnedMembers.
        public int TenantId { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; }
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }
        public DateTime AppointmentDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; } = DateTime.MinValue;

        /// <summary>Where the booking is in its lifecycle. New bookings start Pending.</summary>
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        /// <summary>Reason for the visit or a handover note. Optional.</summary>
        public string? Notes { get; set; }
    }
}
