using AutoMapper;
using Clinic.Api.DTOs.AppointmentDto;
using Clinic.Api.DTOs.DoctorDto;
using Clinic.Api.DTOs.PatientDto;
using Clinic.Api.DTOs.ScheduleDto;
using Clinic.Domain.Entites;

namespace Clinic.Api.Helper
{
    /// <summary>
    /// Maps are declared in one direction only.
    ///
    /// The previous profile used ReverseMap() throughout, which generated DTO -> entity maps that
    /// tried to populate navigation collections. Besides failing AssertConfigurationIsValid, those
    /// reverse maps are a mass-assignment surface: a client that guesses the payload shape could
    /// graft entity graphs onto an aggregate. Response DTOs now map entity -> DTO only.
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            #region ValueConverters
            // DoctorSchedule stores times as TimeSpan while the schedule DTOs expose TimeOnly.
            // AutoMapper has no built-in conversion between the two, so every schedule map failed
            // until these were registered. Standardising the underlying types is a separate
            // concern - see TODO #44.
            CreateMap<TimeSpan, TimeOnly>().ConvertUsing(source => TimeOnly.FromTimeSpan(source));
            CreateMap<TimeOnly, TimeSpan>().ConvertUsing(source => source.ToTimeSpan());
            #endregion

            #region PatientMapping
            // Entity -> response DTO.
            CreateMap<Patient, GetPatientDto>();
            CreateMap<Patient, PatientDto>();

            // Request DTO -> entity. The primary key and the navigation collection belong to the
            // domain and to EF, never to the request payload.
            CreateMap<CreatePatientDto, Patient>()
                .IgnoreSystemOwnedMembers()
                // UserId links this record to an account. A request payload must never set it, or a
                // caller could assign someone else's clinical record to their own login - which is
                // precisely the ownership relationship it exists to express.
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.Appointments, opt => opt.Ignore());

            CreateMap<UpdatePatientDto, Patient>()
                .IgnoreSystemOwnedMembers()
                // UserId links this record to an account. A request payload must never set it, or a
                // caller could assign someone else's clinical record to their own login - which is
                // precisely the ownership relationship it exists to express.
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.Appointments, opt => opt.Ignore());
            #endregion

            #region DoctorMapping
            CreateMap<Doctor, GetDoctorDto>();
            CreateMap<Doctor, DoctorDto>();

            CreateMap<CreateDoctorDto, Doctor>()
                .IgnoreSystemOwnedMembers()
                // UserId links this record to an account. A request payload must never set it, or a
                // caller could assign someone else's clinical record to their own login - which is
                // precisely the ownership relationship it exists to express.
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.Appointments, opt => opt.Ignore())
                .ForMember(dest => dest.DoctorSchedules, opt => opt.Ignore());

            CreateMap<UpdateDoctorDto, Doctor>()
                .IgnoreSystemOwnedMembers()
                // UserId links this record to an account. A request payload must never set it, or a
                // caller could assign someone else's clinical record to their own login - which is
                // precisely the ownership relationship it exists to express.
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.Appointments, opt => opt.Ignore())
                .ForMember(dest => dest.DoctorSchedules, opt => opt.Ignore());
            #endregion

            #region AppointmentMapping
            // The direction the controllers actually use. It did not exist before: the profile only
            // declared AppointmentDto -> Appointment, so every _mapper.Map<AppointmentDto>(entity)
            // call threw AutoMapperMappingException and all five appointment endpoints returned 500.
            //
            // Doctor/Patient are flattened to their names. AutoMapper null-checks the source member
            // chain, so an appointment whose navigations were not Included yields null names rather
            // than a NullReferenceException.
            CreateMap<Appointment, AppointmentDto>()
                .ForMember(dest => dest.DoctorName, opt => opt.MapFrom(src => src.Doctor.Name))
                .ForMember(dest => dest.PatientName, opt => opt.MapFrom(src => src.Patient.Name));

            // The reverse (AppointmentDto -> Appointment) has been removed. It attempted to assign a
            // string DoctorName to the Doctor navigation property, which cannot work and which no
            // controller ever needed.

            CreateMap<CreateAppointmentDto, Appointment>()
                .IgnoreSystemOwnedMembers()
                .ForMember(dest => dest.Doctor, opt => opt.Ignore())
                .ForMember(dest => dest.Patient, opt => opt.Ignore())
                .ForMember(dest => dest.StartTime, opt => opt.Ignore())
                .ForMember(dest => dest.EndTime, opt => opt.Ignore());

            CreateMap<UpdateAppointmentDto, Appointment>()
                .IgnoreSystemOwnedMembers()
                .ForMember(dest => dest.Doctor, opt => opt.Ignore())
                .ForMember(dest => dest.Patient, opt => opt.Ignore())
                .ForMember(dest => dest.StartTime, opt => opt.Ignore())
                .ForMember(dest => dest.EndTime, opt => opt.Ignore());
            #endregion

            #region Schedule
            // The entity calls it DayOfWeek, the DTOs call it WeekDay, so convention-based matching
            // never linked them and the destination member was left unmapped.
            CreateMap<DoctorSchedule, DoctorScheduleDto>()
                .ForMember(dest => dest.DoctorName, opt => opt.MapFrom(src => src.Doctor.Name))
                .ForMember(dest => dest.WeekDay, opt => opt.MapFrom(src => src.DayOfWeek));

            CreateMap<CreateDoctorScheduleDto, DoctorSchedule>()
                .IgnoreSystemOwnedMembers()
                .ForMember(dest => dest.Doctor, opt => opt.Ignore())
                .ForMember(dest => dest.DayOfWeek, opt => opt.MapFrom(src => src.WeekDay));

            // UpdateDoctorScheduleDto carries no DoctorId: a schedule cannot be reassigned to a
            // different doctor through an update.
            CreateMap<UpdateDoctorScheduleDto, DoctorSchedule>()
                .IgnoreSystemOwnedMembers()
                .ForMember(dest => dest.Doctor, opt => opt.Ignore())
                .ForMember(dest => dest.DoctorId, opt => opt.Ignore())
                .ForMember(dest => dest.DayOfWeek, opt => opt.MapFrom(src => src.WeekDay));
            #endregion
        }
    }
}
