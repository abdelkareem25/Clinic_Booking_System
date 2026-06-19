using FluentValidation;

namespace Clinic.Api.DTOs.PatientDto.Validators
{
    public class CreatePatientValidators : AbstractValidator<CreatePatientDto>
    {
        public CreatePatientValidators()
        {
            RuleFor(p => p.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
            RuleFor(p => p.Phone)
                .NotEmpty().WithMessage("Phone is required.")
                .MaximumLength(15).WithMessage("Phone cannot exceed 15 characters.");
        }
    }
}
