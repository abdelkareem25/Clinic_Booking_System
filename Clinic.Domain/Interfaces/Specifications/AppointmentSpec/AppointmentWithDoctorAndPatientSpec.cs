using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentWithDoctorAndPatientSpec : BaseSpecification<Appointment>
    {
        public AppointmentWithDoctorAndPatientSpec()
        {
            Includes.Add(a => a.Doctor.Name);
            Includes.Add(a => a.Patient.Name);
        }
        public AppointmentWithDoctorAndPatientSpec(int id) : base(a => a.Id == id)
        {
            Includes.Add(a => a.Doctor.Name);
            Includes.Add(a => a.Patient.Name);
        }
    }
}
