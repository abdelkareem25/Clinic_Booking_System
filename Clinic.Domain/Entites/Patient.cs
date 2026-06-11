namespace Clinic.Domain.Entites
{
    public class Patient :BaseEntity
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }

        public ICollection<Appointment> Appointments { get; set; } = new HashSet<Appointment>();
    }
}
