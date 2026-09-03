namespace Clinic.Api.DTOs.TenantDto
{
    /// <summary>
    /// A clinic as the API reports it.
    ///
    /// Carries no counts, no roster and no clinical data of any kind - a tenant is an
    /// administrative record, and everything belonging to it is reachable only through the
    /// tenant-filtered endpoints by someone who is actually a member of it.
    /// </summary>
    public class TenantDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public bool IsActive { get; set; }

        /// <summary>Maps from BaseEntity.CreatedAtUtc, which the context stamps on insert.</summary>
        public DateTimeOffset CreatedAtUtc { get; set; }
    }
}
