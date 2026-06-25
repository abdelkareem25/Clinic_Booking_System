using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.ScheduleSpec
{
    public class DoctorScheduleSpecParams : PaginationParams
    {
        public int? DoctorId { get; set; }

        public WeekDay? WeekDay { get; set; }

        public string? Sort { get; set; }
    }
}
