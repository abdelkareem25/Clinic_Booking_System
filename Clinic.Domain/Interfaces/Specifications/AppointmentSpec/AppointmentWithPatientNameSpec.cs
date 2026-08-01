using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentWithPatientNameSpec : BaseSpecification<Appointment>
    {
        public AppointmentWithPatientNameSpec()
        {
            AddInclude(a => a.Patient);
            AddInclude(a => a.Doctor);
        }

        public AppointmentWithPatientNameSpec(string patientName) : base(p => p.Patient.Name == patientName)
        {
            AddInclude(a => a.Patient);
            AddInclude(a => a.Doctor);
        }
    }
}
