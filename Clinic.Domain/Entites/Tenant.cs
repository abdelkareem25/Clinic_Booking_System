namespace Clinic.Domain.Entites
{
    /// <summary>
    /// One clinic: the isolation boundary every <see cref="ITenantEntity"/> is filtered by.
    ///
    /// Global rather than tenant-owned, necessarily - a row cannot be filtered by itself.
    ///
    /// Derives from BaseEntity like every other entity here, so it gets the same int key, the same
    /// RowVersion concurrency token and the same audit columns rather than introducing a second
    /// convention nobody expects. Creation time is therefore BaseEntity.CreatedAtUtc, which
    /// ClinicDbContext already stamps on insert; a separate CreatedAt property would be a second,
    /// unmaintained answer to the same question.
    ///
    /// There are deliberately no Doctors/Patients/Appointments collections. The relationship is
    /// configured from the dependent side - Doctor.TenantId and friends - exactly as the link from
    /// a Patient to its AppUser is; see ClinicDbContext.ConfigureIdentityLinks. Nothing in the
    /// application loads a tenant's children through the tenant, and a navigation collection that
    /// no query uses is an accidental full-table load waiting for someone to touch it.
    /// </summary>
    public class Tenant : BaseEntity
    {
        /// <summary>
        /// The tenant that every pre-existing record was assigned to when this system became
        /// multi-tenant, and the one a freshly created database starts with.
        ///
        /// Seeded through HasData in TenantConfig rather than by ClinicIdentityDbContextSeed,
        /// deliberately: HasData is applied by BOTH `dotnet ef database update` and
        /// EnsureCreated, so the production database and every test database get an identical
        /// tenant roster. A default tenant that existed in one but not the other would make the
        /// isolation tests prove nothing about the system that actually ships.
        /// </summary>
        public const int DefaultTenantId = 1;

        /// <summary>The clinic's display name.</summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Whether the tenant may be used.
        ///
        /// Distinct from deletion: a suspended clinic keeps every record it owns, because those
        /// records are clinical history and cannot simply vanish - the same reasoning as
        /// Doctor.IsActive.
        ///
        /// Note honestly what this does NOT yet do: the query filter keys on TenantId alone, so
        /// deactivating a tenant does not by itself stop its existing tokens working. Enforcing
        /// that belongs at the authentication boundary and is a later, separate decision.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
