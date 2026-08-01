using Clinic.Domain.Entites;
using Clinic.Infrastructure.Data.Context;
using Clinic.Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Clinic.Tests.Security
{
    /// <summary>
    /// Regression tests for TODO #15 (finding H17).
    ///
    /// Clinic.Domain.Entites.User declared a plaintext "Password" column, was registered as
    /// DbSet&lt;User&gt; Users, and existed as a real table in SQL Server. No code referenced it - it
    /// was entirely superseded by ASP.NET Identity - but a schema that invites plaintext credential
    /// storage is a breach waiting for whoever finds the column next and populates it in good faith.
    ///
    /// The distinction these tests enforce: a column named "PasswordHash" (Identity's) is fine; a
    /// column named "Password" is not.
    /// </summary>
    public sealed class NoPlaintextCredentialStorageTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ClinicDbContext> _clinicOptions;
        private readonly DbContextOptions<ClinicIdentityDbContext> _identityOptions;

        public NoPlaintextCredentialStorageTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _clinicOptions = new DbContextOptionsBuilder<ClinicDbContext>().UseSqlite(_connection).Options;
            _identityOptions = new DbContextOptionsBuilder<ClinicIdentityDbContext>().UseSqlite(_connection).Options;
        }

        [Fact]
        public void The_Legacy_User_Entity_No_Longer_Exists()
        {
            var legacy = typeof(BaseEntity).Assembly.GetType("Clinic.Domain.Entites.User");

            Assert.True(legacy is null,
                "Clinic.Domain.Entites.User is back. It stored a plaintext password and is superseded " +
                "by ASP.NET Identity's AppUser.");
        }

        [Fact]
        public void No_Domain_Entity_Declares_A_Plaintext_Password_Property()
        {
            var offenders = typeof(BaseEntity).Assembly
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false })
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Select(p => (Type: t, Property: p)))
                .Where(x => string.Equals(x.Property.Name, "Password", StringComparison.OrdinalIgnoreCase))
                .Select(x => $"{x.Type.FullName}.{x.Property.Name} stores a credential in the clear. " +
                             "Credentials belong in ASP.NET Identity, hashed.")
                .ToList();

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void The_Clinic_Model_Maps_No_Password_Column()
        {
            using var context = new ClinicDbContext(_clinicOptions);

            var offenders = context.Model.GetEntityTypes()
                .SelectMany(e => e.GetProperties().Select(p => (Entity: e, Property: p)))
                .Where(x => string.Equals(x.Property.Name, "Password", StringComparison.OrdinalIgnoreCase))
                .Select(x => $"{x.Entity.Name}.{x.Property.Name}")
                .ToList();

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void The_Clinic_Model_Maps_No_Users_Entity()
        {
            using var context = new ClinicDbContext(_clinicOptions);

            Assert.DoesNotContain(context.Model.GetEntityTypes(),
                e => e.GetTableName() == "Users");
        }

        [Fact]
        public void The_Clinic_Context_Exposes_No_Users_DbSet()
        {
            var offenders = typeof(ClinicDbContext)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType
                         && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)
                         && p.Name == "Users")
                .Select(p => p.Name)
                .ToList();

            Assert.True(offenders.Count == 0,
                "ClinicDbContext exposes a Users DbSet again; user accounts belong to ClinicIdentityDbContext.");
        }

        [Fact]
        public void Creating_The_Clinic_Schema_Produces_No_Users_Table()
        {
            // Model-level assertions can miss a table introduced some other way. This builds the
            // real schema and inspects it.
            using var context = new ClinicDbContext(_clinicOptions);
            context.Database.EnsureCreated();

            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

            var tables = new List<string>();
            using (var reader = command.ExecuteReader())
                while (reader.Read()) tables.Add(reader.GetString(0));

            Assert.DoesNotContain("Users", tables);
            Assert.Contains("Patients", tables);          // the rest of the schema is intact
            Assert.Contains("Doctors", tables);
            Assert.Contains("Appointments", tables);
            Assert.Contains("DoctorSchedules", tables);
        }

        [Fact]
        public void Identity_Still_Stores_A_Hash_And_Not_A_Password()
        {
            // The point is not "no credential storage" - it is that the only credential storage is
            // Identity's, and it is a hash.
            using var context = new ClinicIdentityDbContext(_identityOptions);

            var appUser = context.Model.GetEntityTypes()
                .Single(e => e.ClrType == typeof(Domain.Entites.Identity.AppUser));

            var propertyNames = appUser.GetProperties().Select(p => p.Name).ToList();

            Assert.Contains("PasswordHash", propertyNames);
            Assert.DoesNotContain("Password", propertyNames);
        }

        public void Dispose() => _connection.Dispose();
    }
}
