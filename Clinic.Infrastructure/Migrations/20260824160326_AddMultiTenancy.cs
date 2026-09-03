using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Clinic.Infrastructure.Migrations
{
    /// <summary>
    /// Introduces multi-tenancy over a database that already holds real clinical records.
    ///
    /// Up() is HAND-ORDERED and deliberately differs from what the EF scaffolder produced. The
    /// generated version added TenantId as NOT NULL DEFAULT 0 and then added the foreign keys,
    /// which cannot work on a database containing data: every existing row would be stamped with
    /// tenant 0, no tenant 0 exists, and the FK validation then fails - taking the whole migration
    /// down with it and leaving nothing migrated.
    ///
    /// The order below is the standard safe transition:
    ///
    ///     1. create the Tenants table
    ///     2. insert the default tenant
    ///     3. add TenantId as NULLABLE - always valid, whatever the table already contains
    ///     4. backfill every existing row to the default tenant
    ///     5. only now tighten to NOT NULL, once no row can violate it
    ///     6. index
    ///     7. add the foreign keys, which now validate against data that is already correct
    ///
    /// Nothing here drops a table, drops a column or deletes a row. The only writes are the one
    /// INSERT for the default tenant and the UPDATEs that give existing rows an owner.
    /// </summary>
    public partial class AddMultiTenancy : Migration
    {
        /// <summary>
        /// Kept in step with Tenant.DefaultTenantId. Repeated as a literal rather than referenced,
        /// because a migration is a historical record: it must keep describing what it actually did
        /// even if that constant is changed later.
        /// </summary>
        private const int DefaultTenantId = 1;

        /// <summary>The tenant-owned tables. AspNetUsers is handled separately - see Up().</summary>
        private static readonly string[] TenantOwnedTables =
            ["Doctors", "Patients", "Appointments", "DoctorSchedules"];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- 1. The tenant root -------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            // ---- 2. The default tenant, before anything can reference it ------------------
            //
            // Values match TenantConfig.HasData exactly. They must: EnsureCreated seeds from the
            // model and this migration seeds the real database, and the two have to agree or the
            // tests would be exercising a database shape that never ships.
            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "IsActive", "ModifiedAtUtc", "ModifiedBy", "Name", "RowVersion" },
                values: new object[]
                {
                    DefaultTenantId,
                    new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                    null,
                    true,
                    null,
                    null,
                    "Default Clinic",
                    new Guid("0195a5c6-9e2b-7f42-b8d1-5c9f0a3e7d10")
                });

            // ---- 3. Add the column as NULLABLE -------------------------------------------
            //
            // Valid on a table of any size and any content, because it asserts nothing about the
            // rows already there. This is the step that makes the whole transition safe.
            foreach (var table in TenantOwnedTables)
            {
                migrationBuilder.AddColumn<int>(
                    name: "TenantId",
                    table: table,
                    type: "integer",
                    nullable: true);
            }

            // AspNetUsers gets the column too, and it STAYS nullable past this migration: an
            // account can legitimately belong to no single clinic (platform-level support), and
            // Identity must be able to find an account before any tenant is known - see the note
            // on AppUser.TenantId.
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            // ---- 4. Backfill ---------------------------------------------------------------
            //
            // Everything that exists today was created by, and belongs to, the single clinic this
            // system served before it became multi-tenant. Assigning all of it to the default
            // tenant is what makes the change invisible to current users: they sign in, their token
            // carries tenant 1, and they see exactly the records they saw yesterday.
            foreach (var table in TenantOwnedTables)
            {
                migrationBuilder.Sql(
                    "UPDATE \"" + table + "\" SET \"TenantId\" = " + DefaultTenantId + " WHERE \"TenantId\" IS NULL;");
            }

            // Existing accounts are staff of that same clinic. Without this they would authenticate
            // successfully, carry no tenant claim, and find the application empty - which is the
            // safe failure direction but not an acceptable upgrade experience.
            migrationBuilder.Sql(
                "UPDATE \"AspNetUsers\" SET \"TenantId\" = " + DefaultTenantId + " WHERE \"TenantId\" IS NULL;");

            // ---- 5. Tighten to NOT NULL ----------------------------------------------------
            //
            // Safe only because step 4 guaranteed there is no NULL left. Note there is deliberately
            // NO defaultValue: a database-level default of 0 would silently re-admit exactly the
            // ownerless rows this column exists to forbid.
            foreach (var table in TenantOwnedTables)
            {
                migrationBuilder.AlterColumn<int>(
                    name: "TenantId",
                    table: table,
                    type: "integer",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "integer",
                    oldNullable: true);
            }

            // ---- 6. Indexes ----------------------------------------------------------------
            //
            // PostgreSQL does not index a referencing column automatically, and once the global
            // query filters land every single query in the application filters on this column.
            foreach (var table in TenantOwnedTables)
            {
                migrationBuilder.CreateIndex(
                    name: "IX_" + table + "_TenantId",
                    table: table,
                    column: "TenantId");
            }

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId",
                table: "AspNetUsers",
                column: "TenantId");

            // ---- 7. Foreign keys -----------------------------------------------------------
            //
            // Restrict, emphatically not Cascade: cascading would mean one DELETE against Tenants
            // silently destroys every doctor, patient, appointment and schedule belonging to that
            // clinic. Restrict makes the database refuse instead.
            foreach (var table in TenantOwnedTables)
            {
                migrationBuilder.AddForeignKey(
                    name: "FK_" + table + "_Tenants_TenantId",
                    table: table,
                    column: "TenantId",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            }

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Tenants_TenantId",
                table: "AspNetUsers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Reverses Up() exactly: constraints, then indexes, then the columns, then the table.
        ///
        /// This drops the TenantId columns and the Tenants table - which is what reversing this
        /// migration means - but it destroys no clinical record. Doctors, patients, appointments
        /// and schedules all survive, having simply lost their tenant assignment.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantOwnedTables)
            {
                migrationBuilder.DropForeignKey(name: "FK_" + table + "_Tenants_TenantId", table: table);
                migrationBuilder.DropIndex(name: "IX_" + table + "_TenantId", table: table);
                migrationBuilder.DropColumn(name: "TenantId", table: table);
            }

            migrationBuilder.DropForeignKey(name: "FK_AspNetUsers_Tenants_TenantId", table: "AspNetUsers");
            migrationBuilder.DropIndex(name: "IX_AspNetUsers_TenantId", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "TenantId", table: "AspNetUsers");

            migrationBuilder.DropTable(name: "Tenants");
        }
    }
}
