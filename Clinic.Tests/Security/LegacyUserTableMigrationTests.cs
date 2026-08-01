using Clinic.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Tests.Security
{
    /// <summary>
    /// Tests for TODO #15 (finding H17) covering the migration.
    ///
    /// Removing the entity only changes the model. An existing database still has the table - and
    /// the plaintext column - until a migration drops it, so the migration is the part that
    /// actually closes the finding on a deployed system.
    /// </summary>
    public sealed class LegacyUserTableMigrationTests
    {
        private const string MigrationName = "RemoveLegacyUserTable";

        private static IReadOnlyList<string> ClinicMigrations()
        {
            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlServer("Server=none;Database=none;Trusted_Connection=True")   // never opened
                .Options;

            using var context = new ClinicDbContext(options);

            return context.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        }

        [Fact]
        public void A_Migration_Exists_To_Drop_The_Table()
        {
            Assert.Contains(ClinicMigrations(), name => name.EndsWith(MigrationName, StringComparison.Ordinal));
        }

        [Fact]
        public void The_Drop_Is_The_Most_Recent_Migration()
        {
            // If something were scaffolded after it without the table being gone, the model and the
            // migration history would have diverged.
            Assert.EndsWith(MigrationName, ClinicMigrations().Last(), StringComparison.Ordinal);
        }

        [Fact]
        public void The_Migration_Drops_The_Users_Table()
        {
            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlServer("Server=none;Database=none;Trusted_Connection=True").Options;
            using var context = new ClinicDbContext(options);

            var assembly = context.GetService<IMigrationsAssembly>();
            var key = assembly.Migrations.Keys
                .Single(name => name.EndsWith(MigrationName, StringComparison.Ordinal));

            var migration = assembly.CreateMigration(assembly.Migrations[key], "SqlServer");

            var droppedTables = migration.UpOperations.OfType<DropTableOperation>().Select(o => o.Name);

            Assert.Contains("Users", droppedTables);
        }

        [Fact]
        public void The_Model_Snapshot_No_Longer_Describes_The_Legacy_Entity()
        {
            // A stale snapshot would make the NEXT migration try to drop the table a second time.
            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlServer("Server=none;Database=none;Trusted_Connection=True").Options;
            using var context = new ClinicDbContext(options);

            var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;

            Assert.NotNull(snapshot);
            Assert.DoesNotContain(snapshot!.Model.GetEntityTypes(), e => e.Name.EndsWith(".User", StringComparison.Ordinal));
        }
    }
}
