using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.ScheduleSpec
{
    public class ScheduleCountSpecification : BaseSpecification<DoctorSchedule>
    {
        public ScheduleCountSpecification(DoctorScheduleSpecParams param) :base
            (
                x =>
                (!param.DoctorId.HasValue
                || x.DoctorId == param.DoctorId)
                &&
                (!param.WeekDay.HasValue
                || x.DayOfWeek == param.WeekDay)
            )
        { 
            // No pagination or sorting Cuz its a count 
        }
    }
}
