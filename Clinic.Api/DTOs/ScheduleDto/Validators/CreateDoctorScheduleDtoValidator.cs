using FluentValidation;

namespace Clinic.Api.DTOs.ScheduleDto.Validators
{
    public class CreateDoctorScheduleDtoValidator : AbstractValidator<CreateDoctorScheduleDto>
    {
        public CreateDoctorScheduleDtoValidator()
        {
            RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("End time must be after start time.");
        }
    }
}
