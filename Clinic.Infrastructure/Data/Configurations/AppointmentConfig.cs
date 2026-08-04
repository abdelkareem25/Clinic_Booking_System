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

            // Stored as an int, matching DoctorSchedule.DayOfWeek. Storing the name instead would
            // make renaming an enum member a silent data migration.
            builder.Property(a => a.Status)
                .HasConversion<int>()
                .HasDefaultValue(AppointmentStatus.Pending);

            builder.Property(a => a.Notes)
                .HasMaxLength(1000);

            // The application checks for an overlap and then inserts. Those are two round trips, so
            // two concurrent requests can both pass the check before either writes - the classic
            // time-of-check/time-of-use race, and the one a double-clicked Book button reproduces
            // every time.
            //
            // This index makes the database refuse the second identical booking outright. It cannot
            // express "no overlapping interval" - SQL Server has no exclusion constraints - so it
            // catches exact-start duplicates, which is the overwhelmingly common case. Genuinely
            // overlapping concurrent inserts at DIFFERENT start times still need serializable
            // isolation; see the note in AppointmentsController.
            builder.HasIndex(a => new { a.DoctorId, a.AppointmentDate })
                .IsUnique()
                .HasDatabaseName("UX_Appointments_DoctorId_AppointmentDate");

            // Supports the overlap query, which filters on DoctorId and then on the interval.
            builder.HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.EndTime })
                .HasDatabaseName("IX_Appointments_DoctorId_Interval");
        }
    }
}
