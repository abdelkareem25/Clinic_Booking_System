using Clinic.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Data.Configurations
{
    public class DoctorScheduleConfig : IEntityTypeConfiguration<DoctorSchedule>
    {
        public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
        {
            builder.HasOne(ds => ds.Doctor)
                .WithMany(d => d.DoctorSchedules)
                .HasForeignKey(ds => ds.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Property(x => x.DayOfWeek)
                .HasConversion<int>();
            builder.Property(x => x.StartTime)
                .IsRequired();
            builder.Property(x => x.EndTime)
                .IsRequired();

            // MULTI-TENANT: covers AppointmentRepository.IsWithinWorkingHoursAsync exactly, which
            // runs on EVERY booking and every reschedule. Its WHERE clause is now
            //
            //     TenantId = @me AND DoctorId = @doctor AND DayOfWeek = @day
            //
            // and these are those three columns in that order, so the whole predicate is answered
            // from the index. StartTime and EndTime are deliberately absent: that comparison is
            // done in memory over the handful of blocks a doctor works on one weekday, precisely
            // because TimeSpan is not translatable on every provider - see the note on that method.
            //
            // Supersedes the standalone (TenantId) index; ConfigureTenantLinks detects the prefix.
            builder.HasIndex(s => new { s.TenantId, s.DoctorId, s.DayOfWeek });
        }
    }
}
