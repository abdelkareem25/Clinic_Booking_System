using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TuneTenantIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DoctorSchedules_TenantId",
                table: "DoctorSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_IsActive",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_TenantId",
                table: "Doctors");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_TenantId_DoctorId_DayOfWeek",
                table: "DoctorSchedules",
                columns: new[] { "TenantId", "DoctorId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_TenantId_IsActive",
                table: "Doctors",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DoctorSchedules_TenantId_DoctorId_DayOfWeek",
                table: "DoctorSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Doctors_TenantId_IsActive",
                table: "Doctors");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_TenantId",
                table: "DoctorSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_IsActive",
                table: "Doctors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_TenantId",
                table: "Doctors",
                column: "TenantId");
        }
    }
}
