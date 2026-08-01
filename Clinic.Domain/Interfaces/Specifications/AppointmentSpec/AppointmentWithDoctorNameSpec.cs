using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentWithDoctorNameSpec: BaseSpecification<Appointment>
    {
        public AppointmentWithDoctorNameSpec()
        {
            AddInclude(a => a.Doctor);
        }

        // a.Doctor.Name in the CRITERIA is legitimate: it translates to a join plus a WHERE clause.
        // Only Include is restricted to navigation properties.
        public AppointmentWithDoctorNameSpec(string doctorName) : base(a => a.Doctor.Name == doctorName)
        {
            AddInclude(a => a.Doctor);

            // The response DTO also exposes PatientName, so the patient has to be loaded as well or
            // AppointmentDto.PatientName comes back null for every row.
            AddInclude(a => a.Patient);
        }
    }
}
