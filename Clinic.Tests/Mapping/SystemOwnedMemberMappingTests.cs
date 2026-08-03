using AutoMapper;
using Clinic.Api.DTOs.AppointmentDto;
using Clinic.Api.DTOs.DoctorDto;
using Clinic.Api.DTOs.PatientDto;
using Clinic.Api.DTOs.ScheduleDto;
using Clinic.Api.Helper;
using Clinic.Domain.Entites;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clinic.Tests.Mapping
{
    /// <summary>
    /// Guards for TODO #21 (finding H18).
    ///
    /// The audit columns and the concurrency token are owned by ClinicDbContext. If a request DTO
    /// could map onto them, a caller could forge who created a record, backdate it, or supply a
    /// stale RowVersion and defeat the concurrency check it exists to enforce.
    /// </summary>
    public sealed class SystemOwnedMemberMappingTests
    {
        private static readonly IMapper Mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

        [Fact]
        public void A_Create_Request_Cannot_Set_The_Audit_Columns()
        {
            var existing = new Doctor
            {
                Id = 7,
                CreatedBy = "the-real-author",
                CreatedAtUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                RowVersion = Guid.NewGuid()
            };
            var originalVersion = existing.RowVersion;

            Mapper.Map(new UpdateDoctorDto { Id = 999, Name = "Dr. X", Specialization = "Y" }, existing);

            Assert.Equal(7, existing.Id);
            Assert.Equal("the-real-author", existing.CreatedBy);
            Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), existing.CreatedAtUtc);
            Assert.Equal(originalVersion, existing.RowVersion);
        }

        [Fact]
        public void A_New_Entity_From_A_Request_Has_No_Audit_Values_Of_Its_Own()
        {
            // They are stamped by the context on save, not carried in from the wire.
            var doctor = Mapper.Map<Doctor>(new CreateDoctorDto { Id = 5, Name = "Dr. X", Specialization = "Y" });

            Assert.Equal(0, doctor.Id);
            Assert.Equal(Guid.Empty, doctor.RowVersion);
            Assert.Equal(default, doctor.CreatedAtUtc);
            Assert.Null(doctor.CreatedBy);
            Assert.Null(doctor.ModifiedAtUtc);
            Assert.Null(doctor.ModifiedBy);
        }

        public static TheoryData<Type, Type> RequestMaps() => new()
        {
            { typeof(CreatePatientDto), typeof(Patient) },
            { typeof(UpdatePatientDto), typeof(Patient) },
            { typeof(CreateDoctorDto), typeof(Doctor) },
            { typeof(UpdateDoctorDto), typeof(Doctor) },
            { typeof(CreateAppointmentDto), typeof(Appointment) },
            { typeof(UpdateAppointmentDto), typeof(Appointment) },
            { typeof(CreateDoctorScheduleDto), typeof(DoctorSchedule) },
            { typeof(UpdateDoctorScheduleDto), typeof(DoctorSchedule) }
        };

        [Theory]
        [MemberData(nameof(RequestMaps))]
        public void No_Request_Map_Can_Touch_A_System_Owned_Member(Type source, Type destination)
        {
            // Asserted behaviourally rather than by inspecting AutoMapper's internal type maps: this
            // is what actually matters, and it does not break when AutoMapper reshuffles its API.
            var createdAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var modifiedAt = new DateTimeOffset(2021, 2, 2, 0, 0, 0, TimeSpan.Zero);
            var version = Guid.NewGuid();

            var entity = (BaseEntity)Activator.CreateInstance(destination)!;
            entity.Id = 42;
            entity.RowVersion = version;
            entity.CreatedAtUtc = createdAt;
            entity.CreatedBy = "the-real-author";
            entity.ModifiedAtUtc = modifiedAt;
            entity.ModifiedBy = "the-real-editor";

            Mapper.Map(Activator.CreateInstance(source)!, entity, source, destination);

            Assert.Equal(42, entity.Id);
            Assert.Equal(version, entity.RowVersion);
            Assert.Equal(createdAt, entity.CreatedAtUtc);
            Assert.Equal("the-real-author", entity.CreatedBy);
            Assert.Equal(modifiedAt, entity.ModifiedAtUtc);
            Assert.Equal("the-real-editor", entity.ModifiedBy);
        }
    }
}
