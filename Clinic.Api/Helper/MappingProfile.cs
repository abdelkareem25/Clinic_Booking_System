using AutoMapper;
using Clinic.Api.DTOs.AppointmentDto;
using Clinic.Api.DTOs.DoctorDto;
using Clinic.Api.DTOs.PatientDto;
using Clinic.Api.DTOs.ScheduleDto;
using Clinic.Domain.Entites;

namespace Clinic.Api.Helper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            #region PatientMapping
            CreateMap<Patient, GetPatientDto>().ReverseMap();
            CreateMap<CreatePatientDto, Patient>().ReverseMap();
            CreateMap<UpdatePatientDto, Patient>().ReverseMap();
            CreateMap<PatientDto, Patient>().ReverseMap();

            #endregion
            #region DoctorMapping
            CreateMap<Doctor, GetDoctorDto>().ReverseMap();
            CreateMap<CreateDoctorDto, Doctor>().ReverseMap();
            CreateMap<UpdateDoctorDto, Doctor>().ReverseMap();
            CreateMap<DoctorDto, Doctor>().ReverseMap();
            #endregion
            #region UserMapping

            #endregion
            #region AppointmentMapping
            // CreateMap<Appointment, GetAppointmentDto>().ReverseMap();
            CreateMap<CreateAppointmentDto, Appointment>();//from CreateAppointmentDto to Appointment 
            CreateMap<UpdateAppointmentDto, Appointment>();//from UpdateAppointmentDto to Appointment
            CreateMap<AppointmentDto, Appointment>()
                .ForMember(dest => dest.Doctor, opt => opt.MapFrom(src => src.DoctorName))
                .ForMember(dest => dest.Patient, opt => opt.MapFrom(src => src.PatientName));
            #endregion
            #region Schedule
            CreateMap<DoctorSchedule, DoctorScheduleDto>()
                .ForMember(dest => dest.DoctorName,
               opt => opt.MapFrom(src => src.Doctor.Name));
            CreateMap<CreateDoctorScheduleDto, DoctorSchedule>();

            CreateMap<UpdateDoctorScheduleDto, DoctorSchedule>();
            #endregion
        }
    }
}
