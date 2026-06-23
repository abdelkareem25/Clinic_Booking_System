using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.PatientSpec
{
    public class PatientCountSpecification : BaseSpecification<Patient>
    {
        public PatientCountSpecification(PatientSpecParams CountParam):base(
            p=>string.IsNullOrEmpty(CountParam.Search) || p.Name.ToLower().Contains(CountParam.Search.ToLower())
            )
        {

        }
    }
}
