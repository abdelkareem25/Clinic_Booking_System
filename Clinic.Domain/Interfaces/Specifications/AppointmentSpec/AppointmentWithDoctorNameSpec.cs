using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentWithDoctorNameSpec: BaseSpecification<Appointment>
    {
        public AppointmentWithDoctorNameSpec()
        {
            Includes.Add(a => a.Doctor.Name);
        }
        public AppointmentWithDoctorNameSpec(string doctorName) : base(a => a.Doctor.Name == doctorName)
        {
            Includes.Add(a => a.Doctor);
        }
    }
}
