using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentSpecParams : PaginationParams
    {
        public int? DoctorId { get; set; }

        public int? PatientId { get; set; }

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
