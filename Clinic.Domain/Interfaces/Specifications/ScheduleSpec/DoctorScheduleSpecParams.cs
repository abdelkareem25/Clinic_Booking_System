using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.ScheduleSpec
{
    public class DoctorScheduleSpecParams : PaginationParams
    {
        /// <summary>
        /// The whole rota is one bounded read: at most a handful of shifts per doctor per week.
        /// Twenty rows covers under three doctors, so any screen that reasons about working hours
        /// - the calendar's open/closed shading, the booking form's available days - was drawing
        /// conclusions from a fraction of the schedule and treating the rest as "does not work".
        /// </summary>
        protected override int MaxPageSize => 500;

        public int? DoctorId { get; set; }

        public WeekDay? WeekDay { get; set; }

        public string? Sort { get; set; }
    }
}
