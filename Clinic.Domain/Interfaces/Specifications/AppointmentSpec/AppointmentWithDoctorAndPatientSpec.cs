using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentWithDoctorAndPatientSpec : BaseSpecification<Appointment>
    {
        public AppointmentWithDoctorAndPatientSpec()
        {
            // Include the navigation properties themselves, never their scalar members.
            // a => a.Doctor.Name compiled (string boxes to object) but EF Core rejected it with
            // "The expression 'a => a.Doctor.Name' is invalid inside an 'Include' operation".
            AddInclude(a => a.Doctor);
            AddInclude(a => a.Patient);
        }

        public AppointmentWithDoctorAndPatientSpec(int id) : base(a => a.Id == id)
        {
            AddInclude(a => a.Doctor);
            AddInclude(a => a.Patient);
        }
    }
}
