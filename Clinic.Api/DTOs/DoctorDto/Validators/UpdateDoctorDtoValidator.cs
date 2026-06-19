using FluentValidation;

namespace Clinic.Api.DTOs.DoctorDto.Validators
{
    public class UpdateDoctorDtoValidator : AbstractValidator<UpdateDoctorDto>
    {
        public UpdateDoctorDtoValidator()
        {
            RuleFor(X=>X.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
            RuleFor(X => X.Specialization)
                .NotEmpty().WithMessage("Specialization is required.")
                .MaximumLength(100).WithMessage("Specialization must not exceed 100 characters.");
        }
    }
}
