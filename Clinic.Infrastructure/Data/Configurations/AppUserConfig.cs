using Clinic.Domain.Entites.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Data.Configurations
{
    /// <summary>
    /// The account-administration columns added on top of IdentityUser.
    ///
    /// Deliberately NO global query filter on IsDeleted. A filter would hide soft-deleted rows from
    /// UserManager.FindByEmailAsync too, so re-provisioning a deleted colleague's address would look
    /// available and then fail against the unique index with an error nobody can interpret.
    /// AccountsController filters explicitly instead, and can therefore say what actually happened.
    /// </summary>
    public class AppUserConfig : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> builder)
        {
            builder.Property(u => u.DisplayName)
                .HasMaxLength(100);

            // See DoctorConfig for why the sentinel is set. Without it, provisioning an account
            // with IsActive = false would silently create an ACTIVE account, because EF would omit
            // the column and the database default would win.
            builder.Property(u => u.IsActive)
                .HasDefaultValue(true)
                .HasSentinel(true);

            // No sentinel needed here: the CLR default (false) already matches the database
            // default, so a true is always sent.
            builder.Property(u => u.IsDeleted)
                .HasDefaultValue(false);

            // The accounts list pages over live rows only, so this is the access path that matters.
            builder.HasIndex(u => u.IsDeleted);
        }
    }
}
