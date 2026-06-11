namespace Clinic.Domain.Entites
{
    public class DoctorSchedule : BaseEntity
    {
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }
        public WeekDay DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
