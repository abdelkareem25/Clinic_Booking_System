namespace Clinic.Domain.Entites
{
    public class Doctor : BaseEntity
    {
        public string Name { get; set; }
        public string Specialization { get; set; }
        public ICollection<DoctorSchedule> DoctorSchedules { get; set; } = new HashSet<DoctorSchedule>();
        
        public ICollection<Appointment> Appointments { get; set; } = new HashSet<Appointment>();
    }
}
