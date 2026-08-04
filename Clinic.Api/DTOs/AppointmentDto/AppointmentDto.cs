namespace Clinic.Api.DTOs.AppointmentDto
{
    /// <summary>
    /// The appointment as the client renders it.
    ///
    /// Both the identifier and the display name of each party are carried. The name is what a human
    /// reads; the id is what the client filters and navigates by, and without it the list screen had
    /// to match rows by comparing doctor names as strings - which breaks on two doctors sharing a
    /// name and on any rename.
    /// </summary>
    public class AppointmentDto
    {
        public int Id { get; set; }

        public int DoctorId { get; set; }

        public string DoctorName { get; set; }

        public int PatientId { get; set; }

        public string PatientName { get; set; }

        public DateTime AppointmentDate { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        /// <summary>Serialised as the enum name ("Confirmed"), matching the SPA's string union.</summary>
        public string Status { get; set; }

        public string? Notes { get; set; }
    }
}
