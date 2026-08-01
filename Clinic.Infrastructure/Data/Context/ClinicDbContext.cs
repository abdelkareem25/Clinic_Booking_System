using Clinic.Domain.Entites;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Data.Context
{
    public class ClinicDbContext :DbContext
    {
        public ClinicDbContext(DbContextOptions<ClinicDbContext> options) : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicDbContext).Assembly);
        }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<DoctorSchedule> DoctorSchedules { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

        // There is deliberately no DbSet<User>. The legacy Clinic.Domain.Entites.User entity
        // defined a plaintext 'Password' column and was entirely superseded by ASP.NET Identity
        // (AppUser / AspNetUsers). It was referenced by no code, but a schema that invites plaintext
        // credential storage is an accident waiting for the next developer who finds the column.
        // Removed in TODO #15 (finding H17); RemoveLegacyUserTable drops the table.
    }
}
