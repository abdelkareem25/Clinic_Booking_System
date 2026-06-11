using AutoMapper;
using Clinic.Api.DTOs.PatientDto;
using Clinic.Domain.Entites;

namespace Clinic.Api.Helper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Patient, GetPatientDto>().ReverseMap();
        }
    }
}
