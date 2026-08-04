using FluentValidation;

namespace Clinic.Api.DTOs.DoctorDto.Validators
{
    /// <summary>
    /// Rules for a single shift. Rules that need to see the other shifts - duplicates and overlaps -
    /// live on <see cref="CreateDoctorDtoValidator"/>, because they are a property of the set.
    /// </summary>
    public class DoctorShiftDtoValidator : AbstractValidator<DoctorShiftDto>
    {
        public DoctorShiftDtoValidator()
        {
            RuleFor(x => x.WeekDay)
                .IsInEnum().WithMessage("Unknown day of the week.");

            RuleFor(x => x.EndTime)
                .GreaterThan(x => x.StartTime)
                .WithMessage("A shift must end after it starts.");
        }
    }
}
