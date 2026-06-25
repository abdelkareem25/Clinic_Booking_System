using Clinic.Domain.Entites;

namespace Clinic.Api.DTOs.ScheduleDto
{
    public class UpdateDoctorScheduleDto
    {
        public WeekDay WeekDay { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }
    }
}
