using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentSpecParams : PaginationParams
    {
        /// <summary>
        /// A day or a week of the diary is a bounded read, not a browsable list, so the 20-row
        /// ceiling would drop appointments off the end of the calendar instead of paging them.
        /// Combined with <see cref="From"/>/<see cref="To"/> the result set stays small.
        /// </summary>
        protected override int MaxPageSize => 500;

        public int? DoctorId { get; set; }

        public int? PatientId { get; set; }

        /// <summary>
        /// Inclusive lower bound on the appointment start. Optional: absent means "no lower bound",
        /// so every existing caller is unaffected.
        ///
        /// The scheduling calendar loads exactly the day or week on screen. Without this it had to
        /// fetch a wide slice of the table and filter in the browser, which is both slower and
        /// silently lossy once the clinic has more appointments than one page.
        /// </summary>
        public DateTime? From { get; set; }

        /// <summary>Exclusive upper bound on the appointment start. See <see cref="From"/>.</summary>
        public DateTime? To { get; set; }

        /// <summary>
        /// Optional status filter, as the enum name ("Confirmed"). Bound as a string rather than the
        /// enum so an unrecognised value is a no-op filter instead of a model-binding 400 - a stale
        /// bookmark should not break the page.
        /// </summary>
        public string? Status { get; set; }

        public string? Sort { get; set; }

        /// <summary>
        /// <see cref="Status"/> resolved to the enum, or null when absent or unrecognised.
        ///
        /// The specifications compare against this rather than against x.Status.ToString(): calling
        /// ToString() on a column inside a predicate is not translatable to SQL, which is why the
        /// original status filter was commented out instead of working.
        /// </summary>
        public AppointmentStatus? ParsedStatus =>
            Enum.TryParse<AppointmentStatus>(Status, ignoreCase: true, out var parsed) ? parsed : null;
    }
}
