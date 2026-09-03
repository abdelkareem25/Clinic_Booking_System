using Clinic.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Data.Configurations
{
    /// <summary>
    /// The tenant root. Global by necessity - a row cannot be filtered by itself - so this is the
    /// one clinical-side entity that gets no query filter and no TenantId.
    /// </summary>
    public class TenantConfig : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(200);

            // Deliberately NOT unique. Two genuinely separate clinics can share a display name -
            // "City Clinic" is not a rare choice - and a unique constraint here would reject a
            // legitimate tenant for a reason that is a business decision nobody has made yet.
            // Nothing queries tenants by name either, so there is no index to justify on those
            // grounds. If Phase 11 decides duplicates should be refused, that belongs in the
            // creation endpoint where it can say so in words.

            // The default tenant, seeded declaratively so that `dotnet ef database update` and
            // EnsureCreated produce the same tenant roster - see Tenant.DefaultTenantId.
            //
            // Every value is a literal constant, necessarily: HasData is baked into the model
            // snapshot and compared against it on every subsequent migration, so anything derived
            // from DateTimeOffset.UtcNow or Guid.NewGuid would differ on every build and the tools
            // would scaffold a pointless "the seed data changed" migration each time.
            //
            // CreatedBy is left null, which is the truthful answer: no user created this row.
            // BaseEntity.CreatedBy is documented as null for exactly this case.
            builder.HasData(new Tenant
            {
                Id = Tenant.DefaultTenantId,
                Name = "Default Clinic",
                IsActive = true,
                RowVersion = new Guid("0195a5c6-9e2b-7f42-b8d1-5c9f0a3e7d10"),
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });

            // No HasDefaultValue on IsActive, and therefore no HasSentinel trap.
            //
            // DoctorConfig and AppUserConfig both need HasSentinel(true) precisely because they
            // declare a DATABASE default of true: EF then omits the column whenever the property
            // still holds the CLR default (false), so creating a deactivated row would silently
            // write true. Declaring the default in the CLR instead - `IsActive = true` on the
            // property - means the value is always sent explicitly, and the whole class of bug is
            // unreachable rather than merely handled.
        }
    }
}
