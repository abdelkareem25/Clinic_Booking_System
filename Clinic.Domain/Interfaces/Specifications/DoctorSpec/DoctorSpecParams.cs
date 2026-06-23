using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.DoctorSpec
{
    public class DoctorSpecParams :PaginationParams
    {
        public string? Search {  get; set; }
        public string? Specialty { get; set; }
        public string? Sort { get; set; }
    }
}
