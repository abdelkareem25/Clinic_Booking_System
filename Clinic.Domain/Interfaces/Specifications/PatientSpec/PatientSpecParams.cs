using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.PatientSpec
{
    public class PatientSpecParams : PaginationParams
    {
        public string? Search { get; set; }

        public string? Sort { get; set; }

        public int? Age { get; set; }
    }
}
