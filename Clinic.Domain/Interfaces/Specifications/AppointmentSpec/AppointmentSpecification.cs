using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentSpecification:BaseSpecification<Appointment>
    {
        public AppointmentSpecification(AppointmentSpecParams param) :base(x=>
            (!param.DoctorId.HasValue || x.DoctorId == param.DoctorId)
            &&
            (!param.PatientId.HasValue
             || x.PatientId == param.PatientId)
            //&&
            //(string.IsNullOrEmpty(param.Status) || x.Status.ToString() == param.Status)
            )
        {
            switch(param.Sort)
            {
                case "Ascending":
                    AddOrderBy(a=>a.AppointmentDate);
                    break;
                case "Descending":
                    AddOrderByDescending(a=>a.AppointmentDate);
                    break;
                default:
                    AddOrderBy(a=> a.AppointmentDate);
                    break;
            }
            ApplyPagination(param.Skip, param.PageSize);
        }
    }
}

           
           