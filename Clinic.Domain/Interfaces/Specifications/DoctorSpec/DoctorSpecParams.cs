using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.DoctorSpec
{
    public class DoctorSpecParams :PaginationParams
    {
        /// <summary>
        /// A clinic has tens of practitioners, not thousands, and several screens need all of
        /// them at once rather than a page: the booking form's doctor list, the schedule grid and
        /// the calendar's columns. Capped at 20 those screens silently showed the first twenty
        /// doctors and no indication that anyone was missing.
        /// </summary>
        protected override int MaxPageSize => 200;

        public string? Search {  get; set; }
        public string? Specialty { get; set; }
        public string? Sort { get; set; }
    }
}
