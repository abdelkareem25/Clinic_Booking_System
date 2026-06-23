using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.DoctorSpec
{
    public class DoctorWithCountSpecification : BaseSpecification<Doctor>
    {
        public DoctorWithCountSpecification(DoctorSpecParams param) :base(
            D =>
                 (string.IsNullOrEmpty(param.Search) || D.Name.ToLower().Contains(param.Search.ToLower()))
                 &&
                 (string.IsNullOrEmpty(param.Specialty) || D.Specialization == param.Specialty)

            )
        {


        }
    }
}
