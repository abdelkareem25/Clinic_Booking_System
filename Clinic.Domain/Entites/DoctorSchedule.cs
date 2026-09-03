namespace Clinic.Domain.Entites
{
    public class DoctorSchedule : BaseEntity, ITenantEntity
    {
        // MULTI-TENANT: the clinic that owns this record. Assigned centrally by
        // ClinicDbContext on insert - never by a controller and never from a request
        // payload; see AuditMappingExtensions.IgnoreSystemOwnedMembers.
        public int TenantId { get; set; }

        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }
        public WeekDay DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
