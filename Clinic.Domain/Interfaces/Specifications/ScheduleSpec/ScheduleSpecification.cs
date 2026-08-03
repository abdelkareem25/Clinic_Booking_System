using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.ScheduleSpec
{
    public class ScheduleSpecification : BaseSpecification<DoctorSchedule>
    {
        public ScheduleSpecification(DoctorScheduleSpecParams param) : base
            (x =>
                (!param.DoctorId.HasValue
                || x.DoctorId == param.DoctorId)
                &&
                (!param.WeekDay.HasValue
                || x.DayOfWeek == param.WeekDay)
            )
        {
            AddInclude(x => x.Doctor);
            switch (param.Sort)
            {
                case "DayAsc":
                    AddOrderBy(x => x.DayOfWeek);
                    break;
                case "DayDesc":
                    AddOrderByDescending(x => x.DayOfWeek);
                    break;
                default:
                    AddOrderBy(x => x.DayOfWeek);
                    break;
            }
            ApplyPagination(param.Skip, param.PageSize);
        }
    }
}
