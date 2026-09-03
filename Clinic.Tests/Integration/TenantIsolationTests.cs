using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.DoctorSpec;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Repositores;
using Clinic.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests.Integration
{
    /// <summary>
    /// Proof that one clinic cannot reach another clinic's records.
    ///
    /// Deliberately exercised through the SAME components the API uses - ClinicDbContext,
    /// GenericRepository, UnitOfWork and the real specifications - rather than by asserting that a
    /// query filter exists in the model. A filter that is present but not applied on the path the
    /// application actually takes would satisfy the second kind of test and none of the first.
    ///
    /// Nothing here calls IgnoreQueryFilters to arrange or to assert. Setting a test up by
    /// bypassing the mechanism under test is how an isolation suite ends up green against a system
    /// that leaks; the two tenants are populated by two contexts that each genuinely believe they
    /// are their own clinic.
    /// </summary>
    public sealed class TenantIsolationTests : IAsyncLifetime
    {
        private const int TenantA = Tenant.DefaultTenantId;   // 1, seeded by HasData
        private const int TenantB = 2;

        private SqliteConnection _connection = default!;
        private DbContextOptions<ClinicDbContext> _options = default!;

        private int _doctorOfA;
        private int _doctorOfB;
        private int _patientOfA;

        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;

            // EnsureCreated applies TenantConfig.HasData, so tenant 1 already exists.
            await using (var schema = ContextFor(TenantA))
            {
                await schema.Database.EnsureCreatedAsync();

                // Tenant is global - not an ITenantEntity - so it is neither filtered nor stamped,
                // and any context may create one. That is what makes a tenant roster possible.
                schema.Tenants.Add(new Tenant { Id = TenantB, Name = "Second Clinic" });
                await schema.SaveChangesAsync();
            }

            // Seeded through two ordinary contexts that differ ONLY in who they think they are.
            // Note neither sets TenantId: the stamping in SaveChanges is doing that, so these
            // arrangements also prove the write half works.
            await using (var a = ContextFor(TenantA))
            {
                var doctor = new Doctor { Name = "Dr. Aya", Specialization = "Cardiology" };
                var patient = new Patient
                {
                    Name = "Sara",
                    Phone = "01000000000",
                    Gender = "Female",
                    DateOfBirth = new DateTime(1995, 4, 12)
                };

                a.Doctors.Add(doctor);
                a.Patients.Add(patient);
                await a.SaveChangesAsync();

                _doctorOfA = doctor.Id;
                _patientOfA = patient.Id;
            }

            await using (var b = ContextFor(TenantB))
            {
                var doctor = new Doctor { Name = "Dr. Omar", Specialization = "Neurology" };
                b.Doctors.Add(doctor);
                await b.SaveChangesAsync();

                _doctorOfB = doctor.Id;
            }
        }

        public Task DisposeAsync()
        {
            _connection.Dispose();
            return Task.CompletedTask;
        }

        private ClinicDbContext ContextFor(int? tenantId) =>
            new(_options, currentTenant: new StubCurrentTenant(tenantId));

        // ---------------------------------------------------------------------------------------
        // The one that matters most
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// EF caches the model once per context type. If the query filter had captured the tenant
        /// VALUE rather than a property on the context, the first context built would freeze its
        /// tenant into the compiled model and every later context - every later request, every
        /// other clinic - would silently inherit it.
        ///
        /// That failure is invisible: no exception, no warning, correct-looking data. It is the
        /// single worst thing that can go wrong in this design, so it gets its own test, and the
        /// two contexts deliberately share one DbContextOptions to guarantee they share one model.
        /// </summary>
        [Fact]
        public async Task Two_Contexts_Sharing_A_Model_Still_See_Their_Own_Tenant()
        {
            await using var a = ContextFor(TenantA);
            await using var b = ContextFor(TenantB);

            var seenByA = await a.Doctors.Select(d => d.Name).ToListAsync();
            var seenByB = await b.Doctors.Select(d => d.Name).ToListAsync();

            Assert.Equal(["Dr. Aya"], seenByA);
            Assert.Equal(["Dr. Omar"], seenByB);
        }

        // ---------------------------------------------------------------------------------------
        // Reads
        // ---------------------------------------------------------------------------------------

        [Fact]
        public async Task A_Tenant_Cannot_List_Another_Tenants_Doctors()
        {
            await using var b = ContextFor(TenantB);

            var doctors = await b.Doctors.ToListAsync();

            Assert.DoesNotContain(doctors, d => d.Id == _doctorOfA);
            Assert.All(doctors, d => Assert.Equal(TenantB, d.TenantId));
        }

        /// <summary>
        /// GenericRepository.GetByIdAsync is implemented with FindAsync, which consults the change
        /// tracker BEFORE it queries. That was flagged during inspection as the one place a
        /// cross-tenant read might slip past the filter, because a tracked entity is returned
        /// without any query running at all.
        ///
        /// This is the test that settles it rather than reasoning about it - and it matters
        /// disproportionately, because GetByIdAsync is what every GetById, Update and Delete action
        /// in the API calls before deciding whether to return 404.
        /// </summary>
        [Fact]
        public async Task A_Tenant_Cannot_Fetch_Another_Tenants_Doctor_By_Id()
        {
            await using var b = ContextFor(TenantB);
            var repository = new GenericRepository<Doctor>(b);

            var stolen = await repository.GetByIdAsync(_doctorOfA);

            Assert.Null(stolen);
        }

        [Fact]
        public async Task A_Tenant_Cannot_Fetch_Another_Tenants_Patient_By_Id()
        {
            await using var b = ContextFor(TenantB);

            var stolen = await b.Patients.FirstOrDefaultAsync(p => p.Id == _patientOfA);

            Assert.Null(stolen);
        }

        /// <summary>
        /// The specification pattern needed no tenant awareness because the filter is applied at
        /// the query root, beneath everything a specification composes. This proves that holds for
        /// the paged read AND for the count that drives the pager - a count that ignored the filter
        /// would leak the size of another clinic's roster even while showing none of its rows.
        /// </summary>
        [Fact]
        public async Task Specifications_And_Counts_Are_Isolated_Without_Knowing_About_Tenants()
        {
            await using var b = ContextFor(TenantB);
            var repository = new GenericRepository<Doctor>(b);

            var page = await repository.GetAllWithSpecAsync(new DoctorSpecification(new DoctorSpecParams()));
            var total = await repository.CountAsync(new DoctorWithCountSpecification(new DoctorSpecParams()));

            Assert.Equal(1, total);
            Assert.Equal("Dr. Omar", Assert.Single(page).Name);
        }

        /// <summary>
        /// Test 6 from the brief: an authenticated caller carrying no tenant claim.
        ///
        /// Null must mean "see nothing", never "see everything". This is the behaviour that makes
        /// a lost or malformed claim a visibly broken feature rather than a silent disclosure.
        /// </summary>
        [Fact]
        public async Task A_Caller_With_No_Tenant_Sees_Nothing()
        {
            await using var nobody = ContextFor(null);

            Assert.Empty(await nobody.Doctors.ToListAsync());
            Assert.Empty(await nobody.Patients.ToListAsync());
        }

        // ---------------------------------------------------------------------------------------
        // Writes
        // ---------------------------------------------------------------------------------------

        [Fact]
        public async Task A_Tenant_Cannot_Update_Another_Tenants_Patient()
        {
            await using (var b = ContextFor(TenantB))
            {
                // The update path begins by loading the record. It is not there, so the API's
                // "if (patient == null) return NotFound()" fires - the record is unreachable, not
                // merely unwritable.
                Assert.Null(await b.Patients.FirstOrDefaultAsync(p => p.Id == _patientOfA));
            }

            await using var a = ContextFor(TenantA);
            Assert.Equal("Sara", (await a.Patients.FirstAsync(p => p.Id == _patientOfA)).Name);
        }

        [Fact]
        public async Task A_Tenant_Cannot_Delete_Another_Tenants_Doctor()
        {
            await using (var b = ContextFor(TenantB))
            {
                var repository = new GenericRepository<Doctor>(b);
                Assert.Null(await repository.GetByIdAsync(_doctorOfA));
            }

            await using var a = ContextFor(TenantA);
            Assert.NotNull(await a.Doctors.FirstOrDefaultAsync(d => d.Id == _doctorOfA));
        }

        /// <summary>
        /// The write half of isolation: a record created without anyone naming a tenant still ends
        /// up owned by the caller's clinic, because ClinicDbContext stamps it.
        /// </summary>
        [Fact]
        public async Task A_New_Record_Is_Stamped_With_The_Callers_Tenant()
        {
            int id;

            await using (var b = ContextFor(TenantB))
            {
                var doctor = new Doctor { Name = "Dr. New", Specialization = "Locum" };
                b.Doctors.Add(doctor);
                await b.SaveChangesAsync();
                id = doctor.Id;
            }

            await using var verification = ContextFor(TenantB);
            Assert.Equal(TenantB, (await verification.Doctors.FirstAsync(d => d.Id == id)).TenantId);
        }

        /// <summary>
        /// A caller must not be able to plant a record inside another clinic by assigning TenantId
        /// itself. AutoMapper already refuses to map it from a request body; this proves the
        /// context overrides it even when something got past that.
        /// </summary>
        [Fact]
        public async Task An_Explicit_Tenant_Is_Overridden_By_The_Callers_Tenant()
        {
            int id;

            await using (var b = ContextFor(TenantB))
            {
                var planted = new Doctor { Name = "Dr. Trojan", Specialization = "X", TenantId = TenantA };
                b.Doctors.Add(planted);
                await b.SaveChangesAsync();
                id = planted.Id;
            }

            await using var a = ContextFor(TenantA);
            Assert.Null(await a.Doctors.FirstOrDefaultAsync(d => d.Id == id));
        }

        /// <summary>
        /// Moving a record between clinics is not an edit. Without this, an update path could undo
        /// every read filter in the application in a single SaveChanges.
        /// </summary>
        [Fact]
        public async Task A_Records_Tenant_Cannot_Be_Changed_By_An_Update()
        {
            await using (var b = ContextFor(TenantB))
            {
                var doctor = await b.Doctors.FirstAsync(d => d.Id == _doctorOfB);
                doctor.Name = "Dr. Omar Renamed";
                doctor.TenantId = TenantA;              // the attempted move
                await b.SaveChangesAsync();
            }

            await using var verification = ContextFor(TenantB);
            var unchanged = await verification.Doctors.FirstAsync(d => d.Id == _doctorOfB);

            Assert.Equal(TenantB, unchanged.TenantId);  // still ours...
            Assert.Equal("Dr. Omar Renamed", unchanged.Name);   // ...and the real edit still applied
        }

        /// <summary>
        /// Writing with no resolvable tenant and no explicit assignment is a bug, and it should say
        /// so. The alternative is TenantId 0, which surfaces later as a foreign key violation
        /// naming a constraint instead of the actual mistake.
        /// </summary>
        [Fact]
        public async Task Saving_With_No_Tenant_And_No_Explicit_Assignment_Throws()
        {
            await using var nobody = ContextFor(null);
            nobody.Doctors.Add(new Doctor { Name = "Dr. Ownerless", Specialization = "None" });

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => nobody.SaveChangesAsync());

            Assert.Contains("no tenant could be resolved", error.Message);
        }

        /// <summary>
        /// Seeding and background work legitimately have no ambient tenant but do know which clinic
        /// they are writing for. That has to keep working, or the migration path and every existing
        /// test fixture would have nowhere to go.
        /// </summary>
        [Fact]
        public async Task Seeding_Work_May_Assign_A_Tenant_Explicitly()
        {
            await using var seeder = ContextFor(null);
            seeder.Doctors.Add(new Doctor { TenantId = TenantA, Name = "Dr. Seeded", Specialization = "General" });

            await seeder.SaveChangesAsync();

            await using var a = ContextFor(TenantA);
            Assert.Contains(await a.Doctors.ToListAsync(), d => d.Name == "Dr. Seeded");
        }
    }
}
