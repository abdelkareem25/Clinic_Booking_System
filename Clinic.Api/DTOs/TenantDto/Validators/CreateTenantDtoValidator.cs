using FluentValidation;

namespace Clinic.Api.DTOs.TenantDto.Validators
{
    public class CreateTenantDtoValidator : AbstractValidator<CreateTenantDto>
    {
        public CreateTenantDtoValidator()
        {
            RuleFor(t => t.Name)
                .NotEmpty().WithMessage("Name is required.")
                // Matches TenantConfig's HasMaxLength(200). A validator that allowed more would
                // turn a clear 400 into a database truncation error the caller cannot act on.
                .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");
        }
    }
}
