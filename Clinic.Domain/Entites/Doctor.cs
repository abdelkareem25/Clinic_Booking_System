namespace Clinic.Domain.Entites
{
    public class Doctor : BaseEntity
    {
        public string Name { get; set; }
        public string Specialization { get; set; }

        /// <summary>The AspNetUsers account for this doctor, when they have one. See Patient.UserId.</summary>
        public string? UserId { get; set; }
        public ICollection<DoctorSchedule> DoctorSchedules { get; set; } = new HashSet<DoctorSchedule>();
        
        public ICollection<Appointment> Appointments { get; set; } = new HashSet<Appointment>();
    }
}
