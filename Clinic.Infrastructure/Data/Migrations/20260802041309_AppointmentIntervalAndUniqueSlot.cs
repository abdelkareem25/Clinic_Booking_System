using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Backfills the appointment interval and adds the slot indexes.
    ///
    /// UX_Appointments_DoctorId_AppointmentDate is the database-level backstop for the
    /// time-of-check/time-of-use race in booking: the application checks for an overlap and then
    /// inserts, and two concurrent requests can both pass the check.
    ///
    /// NOTE: creating a UNIQUE index fails if the table already contains two appointments for the
    /// same doctor at the same instant. Check before deploying:
    ///     SELECT DoctorId, AppointmentDate, COUNT(*)
    ///     FROM Appointments GROUP BY DoctorId, AppointmentDate HAVING COUNT(*) > 1;
    /// Failing loudly is the right behaviour - those rows are exactly the double-bookings this
    /// finding is about, and a human has to decide which one survives.
    /// </summary>
    public partial class AppointmentIntervalAndUniqueSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows predate the interval. Nothing ever populated StartTime/EndTime, so they
            // sit at DateTime.MinValue and would be invisible to the overlap query - a historical
            // appointment would not block its own slot. Give them the default duration.
            migrationBuilder.Sql(@"
                UPDATE [Appointments]
                SET [StartTime] = [AppointmentDate],
                    [EndTime]   = DATEADD(minute, 30, [AppointmentDate])
                WHERE [EndTime] <= [AppointmentDate];");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_Interval",
                table: "Appointments",
                columns: new[] { "DoctorId", "AppointmentDate", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_DoctorId_AppointmentDate",
                table: "Appointments",
                columns: new[] { "DoctorId", "AppointmentDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_Interval",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_Appointments_DoctorId_AppointmentDate",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments",
                column: "DoctorId");
        }
    }
}
