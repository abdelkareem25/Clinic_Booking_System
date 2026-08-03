using Clinic.Domain.Entites;
using Clinic.Domain.Entites.Identity;
using Clinic.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Data.Context
{
    /// <summary>
    /// The single context for the whole application: clinical records and identity together.
    ///
    /// They used to live in two contexts over two physical databases, which had three consequences:
    ///
    ///   - IUnitOfWork covered only the clinical context, so no operation touching both could be
    ///     atomic without a distributed transaction (MSDTC - unavailable on Azure SQL and painful
    ///     everywhere else). Provisioning a doctor's login and their Doctor record was two
    ///     independent commits with no way to undo the first if the second failed.
    ///   - There was no way to answer "which patient is this logged-in user?", because no foreign
    ///     key could cross the database boundary. That is what blocked resource-based ownership
    ///     checks, and why any authenticated user can currently read any patient's records.
    ///   - Two connection strings, two migration histories and two backup/restore units for one
    ///     logically indivisible dataset.
    /// </summary>
    public class ClinicDbContext : IdentityDbContext<AppUser>
    {
        private readonly ICurrentUser? _currentUser;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// The optional parameters are resolved from DI at runtime. They are optional so that the
        /// many places constructing a context directly - tests, design-time tooling - keep working;
        /// audit columns then record no actor, which is the truthful answer for work with no user.
        /// </summary>
        public ClinicDbContext(
            DbContextOptions<ClinicDbContext> options,
            ICurrentUser? currentUser = null,
            TimeProvider? timeProvider = null) : base(options)
        {
            _currentUser = currentUser;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Required: this is what maps AspNetUsers, AspNetRoles and the rest. Omitting it leaves
            // Identity silently unmapped.
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicDbContext).Assembly);

            // Applied to every entity rather than repeated per configuration class, so a new entity
            // cannot be added without concurrency protection.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                         .Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType)))
            {
                var entity = modelBuilder.Entity(entityType.ClrType);

                // IsConcurrencyToken puts the ORIGINAL value into the WHERE clause of every UPDATE
                // and DELETE. If another transaction changed the row first, zero rows match and EF
                // raises DbUpdateConcurrencyException instead of silently overwriting.
                entity.Property(nameof(BaseEntity.RowVersion)).IsConcurrencyToken();

                entity.Property(nameof(BaseEntity.CreatedBy)).HasMaxLength(450);   // Identity key length
                entity.Property(nameof(BaseEntity.ModifiedBy)).HasMaxLength(450);
            }

            ConfigureIdentityLinks(modelBuilder);
        }

        /// <summary>
        /// Links clinical records to the account that represents the same person.
        ///
        /// A real foreign key, only possible now that both live in one database. Deliberately no
        /// navigation property: the clinical entities should not have to know what an AppUser is,
        /// and an ownership check only ever needs the identifier.
        /// </summary>
        private static void ConfigureIdentityLinks(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Patient>(patient =>
            {
                patient.Property(p => p.UserId).HasMaxLength(450);
                patient.HasIndex(p => p.UserId);

                patient.HasOne<AppUser>()
                       .WithMany()
                       .HasForeignKey(p => p.UserId)
                       // Removing a login must not erase the clinical record it pointed at.
                       .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Doctor>(doctor =>
            {
                doctor.Property(d => d.UserId).HasMaxLength(450);
                doctor.HasIndex(d => d.UserId);

                doctor.HasOne<AppUser>()
                      .WithMany()
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampAuditColumns();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            StampAuditColumns();
            return base.SaveChanges();
        }

        /// <summary>
        /// Maintains the audit columns and rotates the concurrency token.
        ///
        /// Centralised here rather than in each controller for the usual reason: anything a caller
        /// has to remember to do, some caller eventually forgets. Doing it in the context means
        /// every write is covered, including ones added later.
        /// </summary>
        private void StampAuditColumns()
        {
            var now = _timeProvider.GetUtcNow();
            var actor = _currentUser?.UserId;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAtUtc = now;
                        entry.Entity.CreatedBy = actor;
                        entry.Entity.RowVersion = Guid.NewGuid();
                        break;

                    case EntityState.Modified:
                        entry.Entity.ModifiedAtUtc = now;
                        entry.Entity.ModifiedBy = actor;

                        // Assigning a new value leaves the ORIGINAL value untouched, which is what
                        // EF puts in the WHERE clause - so the check still tests what was loaded.
                        entry.Entity.RowVersion = Guid.NewGuid();

                        // Creation provenance is written once. Update() marks every property
                        // modified, so without this an update would happily rewrite who created the
                        // record and when.
                        entry.Property(e => e.CreatedAtUtc).IsModified = false;
                        entry.Property(e => e.CreatedBy).IsModified = false;
                        break;
                }
            }
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
