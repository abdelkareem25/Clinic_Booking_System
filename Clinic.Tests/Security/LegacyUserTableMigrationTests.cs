using Clinic.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Tests.Security
{
    /// <summary>
    /// Tests for TODO #15 (finding H17) covering the migrations.
    ///
    /// The legacy Clinic.Domain.Entites.User entity defined a plaintext 'Password' column. Deleting
    /// the entity only changes the model, so these tests guard the schema itself.
    ///
    /// This class previously asserted that a migration named RemoveLegacyUserTable existed and
    /// dropped the table. The PostgreSQL port squashed the SQL Server migration history into a
    /// single InitialPostgres, so that migration no longer exists and those assertions failed - not
    /// because the finding regressed, but because the thing they named was gone. What is actually
    /// worth guaranteeing is the OUTCOME: no migration in the assembly produces the table, and
    /// neither the snapshot nor the live model describes it. That holds however the history is
    /// later squashed or rewritten.
    ///
    /// One consequence of the squash is worth recording: a database migrated under the old SQL
    /// Server history still physically has the Users table, and there is no longer a migration that
    /// drops it. That is a data-migration concern for any such database, not something these tests
    /// can assert about this codebase.
    /// </summary>
    public sealed class LegacyUserTableMigrationTests
    {
        private const string LegacyTableName = "Users";

        /// <summary>
        /// A context that is never connected - only the migrations assembly and the model are read,
        /// and building either requires no database.
        /// </summary>
        private static ClinicDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ClinicDbContext>()
                .UseNpgsql("Host=none;Database=none")
                .Options;

            return new ClinicDbContext(options);
        }

        [Fact]
        public void No_Migration_Creates_The_Legacy_Users_Table()
        {
            using var context = CreateContext();

            var assembly = context.GetService<IMigrationsAssembly>();

            // Read from the context rather than hardcoded. The previous version passed the literal
            // "SqlServer" here and kept doing so after the port to Npgsql, which nothing caught
            // because the assertion it fed was already failing for an unrelated reason.
            var activeProvider = context.GetService<IDatabaseProvider>().Name;

            // Guards against this passing vacuously if the migrations assembly ever resolves empty.
            Assert.NotEmpty(assembly.Migrations);

            foreach (var key in assembly.Migrations.Keys)
            {
                var migration = assembly.CreateMigration(assembly.Migrations[key], activeProvider);

                Assert.DoesNotContain(migration.UpOperations.OfType<CreateTableOperation>(),
                    operation => operation.Name == LegacyTableName);
            }
        }

        [Fact]
        public void The_Model_Snapshot_No_Longer_Describes_The_Legacy_Entity()
        {
            // A stale snapshot would make the NEXT migration try to recreate or re-drop the table.
            using var context = CreateContext();

            var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;

            Assert.NotNull(snapshot);
            Assert.DoesNotContain(snapshot!.Model.GetEntityTypes(),
                e => e.Name.EndsWith(".User", StringComparison.Ordinal));
        }

        [Fact]
        public void The_Live_Model_Maps_No_Table_Named_Users()
        {
            // The snapshot and the live model are separate artefacts, and the snapshot is only
            // regenerated when someone adds a migration. Checking the live model catches an entity
            // reintroduced in code before any migration exists for it - and catches it under any
            // entity name, since what matters is the table it lands on.
            using var context = CreateContext();

            Assert.DoesNotContain(context.Model.GetEntityTypes(),
                e => string.Equals(e.GetTableName(), LegacyTableName, StringComparison.Ordinal));
        }
    }
}
