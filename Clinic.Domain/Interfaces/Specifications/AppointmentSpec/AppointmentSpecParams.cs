using Clinic.Domain.Interfaces.Specifications.SpecParams;

namespace Clinic.Domain.Interfaces.Specifications.AppointmentSpec
{
    public class AppointmentSpecParams:PaginationParams
    {
        public int? DoctorId { get; set; }

        public int? PatientId { get; set; }

        public string? Status { get; set; } 

        public string? Sort { get; set; }
    }
}
