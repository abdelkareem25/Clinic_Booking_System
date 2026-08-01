using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Drops the legacy "Users" table.
    ///
    /// Clinic.Domain.Entites.User was fully superseded by ASP.NET Identity (AspNetUsers) and was
    /// referenced by no code, but it defined a plaintext "Password" column that any future
    /// developer could have found and populated in good faith.
    ///
    /// Nothing ever wrote to this table - no code touched the DbSet, and until TODO #1 the
    /// application never called SaveChanges at all - so the drop loses nothing. Down() is left
    /// intact so a failed deployment can roll back, but note that it recreates the plaintext
    /// column: if it is ever run, the table must be dropped again rather than used.
    /// </summary>
    public partial class RemoveLegacyUserTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }
    }
}
