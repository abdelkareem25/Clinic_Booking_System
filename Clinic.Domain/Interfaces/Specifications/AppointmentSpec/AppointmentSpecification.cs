using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentSpecification : BaseSpecification<Appointment>
    {
        public AppointmentSpecification(AppointmentSpecParams param) : base(x =>
            (!param.DoctorId.HasValue || x.DoctorId == param.DoctorId)
            &&
            (!param.PatientId.HasValue
             || x.PatientId == param.PatientId)
            &&
            (!param.ParsedStatus.HasValue || x.Status == param.ParsedStatus)
            )
        {
            // THE reason the appointments list rendered "null" for every doctor and patient.
            //
            // This specification backs GET /api/appointments - the endpoint the list screen calls -
            // and it was the only appointment specification that never eager-loaded its
            // navigations. AppointmentWithDoctorAndPatientSpec, AppointmentWithDoctorNameSpec and
            // AppointmentWithPatientNameSpec all include both, which is why GetById showed real
            // names while the list did not.
            //
            // Lazy loading is not enabled, so an un-included navigation stays null. AutoMapper
            // null-checks the src.Doctor.Name chain and yields null rather than throwing, so the
            // failure surfaced as data rather than as an error - a null column instead of a 500.
            AddInclude(a => a.Doctor);
            AddInclude(a => a.Patient);

            switch (param.Sort)
            {
                case "Ascending":
                    AddOrderBy(a => a.AppointmentDate);
                    break;
                case "Descending":
                    AddOrderByDescending(a => a.AppointmentDate);
                    break;
                default:
                    AddOrderBy(a => a.AppointmentDate);
                    break;
            }
            ApplyPagination(param.Skip, param.PageSize);
        }
    }
}
