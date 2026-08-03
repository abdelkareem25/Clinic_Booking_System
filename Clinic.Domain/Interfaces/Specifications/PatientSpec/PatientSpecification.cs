using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Specifications.PatientSpec
{
    public class PatientSpecification : BaseSpecification<Patient>
    {
        public PatientSpecification(PatientSpecParams param) : base(
            p=>string.IsNullOrEmpty(param.Search)
            || p.Name.ToLower().Contains(param.Search.ToLower())
            )
        {
            switch (param.Sort)
            {
                case "Asc":
                    AddOrderBy(p => p.Name);
                    break;
                case "Desc":
                    AddOrderByDescending(p => p.Name);
                    break;
                default:
                    AddOrderBy(p => p.Id);
                    break;
            }

            ApplyPagination(param.Skip, param.PageSize);
        }
    }
}
