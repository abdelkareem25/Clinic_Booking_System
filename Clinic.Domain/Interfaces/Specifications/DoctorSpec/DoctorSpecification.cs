using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.DoctorSpec
{
    public class DoctorSpecification : BaseSpecification<Doctor>
    {
        public DoctorSpecification(DoctorSpecParams param)
            : base(D =>
                 (string.IsNullOrEmpty(param.Search) || D.Name.ToLower().Contains(param.Search.ToLower()))
                 &&
                 (string.IsNullOrEmpty(param.Specialty) || D.Specialization == param.Specialty)
                 )
        {
            switch (param.Sort)
            {
                case "nameAsc":
                    AddOrderBy(x => x.Name);
                    break;

                case "nameDesc":
                    AddOrderByDescending(x => x.Name);
                    break;

                default:
                    AddOrderBy(x => x.Id);
                    break;
            }
            ApplyPagination(param.Skip, param.PageSize);
        }
    }
}
