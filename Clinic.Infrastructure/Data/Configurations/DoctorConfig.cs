using Clinic.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Data.Configurations
{
    public class DoctorConfig : IEntityTypeConfiguration<Doctor>
    {
        public void Configure(EntityTypeBuilder<Doctor> builder)
        {
            builder.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(d => d.Specialization)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(d => d.Phone).HasMaxLength(30);
            builder.Property(d => d.Email).HasMaxLength(256);
            builder.Property(d => d.Bio).HasMaxLength(2000);

            // Explicit precision. Without it EF Core emits "No store type was specified for the
            // decimal property 'ConsultationFee'" as a warning and falls back to decimal(18,2)
            // anyway - stating it keeps the build clean and pins the scale to money.
            builder.Property(d => d.ConsultationFee)
                .HasPrecision(18, 2);

            // HasSentinel is load-bearing, not decoration.
            //
            // With a database default of true, EF omits the column from an INSERT whenever the
            // property still holds its *sentinel* - which is the CLR default, false, unless told
            // otherwise. Creating a doctor with IsActive = false would therefore send no value at
            // all and the database would write true: the checkbox would appear to do nothing.
            // Setting the sentinel to true inverts that, so false is always sent explicitly.
            builder.Property(d => d.IsActive)
                .HasDefaultValue(true)
                .HasSentinel(true);

            // The doctor list filters on this whenever booking is involved.
            builder.HasIndex(d => d.IsActive);
        }
    }
}
