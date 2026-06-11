using Clinic.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Data.Configurations
{
    public class AppointmentConfig : IEntityTypeConfiguration<Appointment>
    {
        public void Configure(EntityTypeBuilder<Appointment> builder)
        {
            builder.HasOne(p => p.Patient)
                .WithMany(ap => ap.Appointments)
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.HasOne(D => D.Doctor)
                .WithMany(A => A.Appointments)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
