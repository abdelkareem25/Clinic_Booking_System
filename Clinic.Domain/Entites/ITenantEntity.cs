namespace Clinic.Domain.Entites
{
    /// <summary>
    /// Marks an entity as belonging to exactly one tenant.
    ///
    /// This is not documentation - it is the switch the persistence layer reads. Implementing it is
    /// what gives an entity a global query filter and what makes SaveChanges stamp its TenantId, so
    /// an entity that does NOT implement it is global, shared by every tenant. That has to be a
    /// deliberate act rather than an omission, which is why the marker is the thing being tested
    /// rather than a hand-maintained list of type names somewhere in Infrastructure.
    ///
    /// Deliberately NOT implemented by AppUser - see the note on AppUser.TenantId for why filtering
    /// the Identity tables by tenant would break every sign-in.
    /// </summary>
    public interface ITenantEntity
    {
        /// <summary>
        /// The owning tenant. Assigned centrally by ClinicDbContext on insert, never by a
        /// controller and never from a request payload; see
        /// AuditMappingExtensions.IgnoreSystemOwnedMembers.
        /// </summary>
        int TenantId { get; set; }
    }
}
