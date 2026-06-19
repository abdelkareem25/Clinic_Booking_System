using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentWithPatientNameSpec : BaseSpecification<Appointment>
    {
        public AppointmentWithPatientNameSpec()
        {
            Includes.Add(a => a.Patient.Name);
        } 
        public AppointmentWithPatientNameSpec(string patientName):base(p=>p.Patient.Name==patientName)
        {
            Includes.Add(a => a.Patient);
            Includes.Add(a=>a.Doctor);
        } 


    }
}
