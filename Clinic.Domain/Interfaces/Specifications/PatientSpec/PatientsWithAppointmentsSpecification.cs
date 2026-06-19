using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.PatientSpec
{
    public class PatientsWithAppointmentsSpecification : BaseSpecification<Patient>
    {
        // get all patients with appointments
        public PatientsWithAppointmentsSpecification() : base()
        {
            // Includes.Add(p => p.Appointments);
        }
    }
}
