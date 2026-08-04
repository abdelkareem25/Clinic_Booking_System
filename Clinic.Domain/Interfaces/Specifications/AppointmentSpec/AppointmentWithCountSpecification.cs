using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    /// <summary>
    /// The same filters as <see cref="AppointmentSpecification"/> without paging or includes, so it
    /// yields the true total. The criteria MUST stay in step with that class, or the pager reports a
    /// count for a different set of rows than the page it accompanies.
    /// </summary>
    public class AppointmentWithCountSpecification : BaseSpecification<Appointment>
    {
        public AppointmentWithCountSpecification(AppointmentSpecParams param) : base(x =>
            (!param.DoctorId.HasValue || x.DoctorId == param.DoctorId)
            &&
            (!param.PatientId.HasValue
             || x.PatientId == param.PatientId)
            &&
            (!param.ParsedStatus.HasValue || x.Status == param.ParsedStatus)
            )
        {
        }
    }
}
