using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentWithCountSpecification : BaseSpecification<Appointment>
    {
        public AppointmentWithCountSpecification(AppointmentSpecParams param) : base(x =>
            (!param.DoctorId.HasValue || x.DoctorId == param.DoctorId)
            &&
            (!param.PatientId.HasValue
             || x.PatientId == param.PatientId)
            //&&
            //(string.IsNullOrEmpty(param.Status) || x.Status.ToString() == param.Status)
            )
        {
        }
    }
}
