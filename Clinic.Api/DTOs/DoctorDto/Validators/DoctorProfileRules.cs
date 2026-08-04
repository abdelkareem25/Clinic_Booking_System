using FluentValidation;

namespace Clinic.Api.DTOs.DoctorDto.Validators
{
    /// <summary>
    /// The profile rules shared by <see cref="CreateDoctorDtoValidator"/> and
    /// <see cref="UpdateDoctorDtoValidator"/>. See <see cref="IDoctorProfileFields"/> for why they
    /// are stated in one place.
    /// </summary>
    public abstract class DoctorProfileValidator<T> : AbstractValidator<T>
        where T : IDoctorProfileFields
    {
        protected DoctorProfileValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

            RuleFor(x => x.Specialization)
                .NotEmpty().WithMessage("Specialization is required.")
                .MaximumLength(100).WithMessage("Specialization must not exceed 100 characters.");

            RuleFor(x => x.Phone)
                .MaximumLength(30).WithMessage("Phone must not exceed 30 characters.")
                .When(x => !string.IsNullOrWhiteSpace(x.Phone));

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Enter a valid email address.")
                .MaximumLength(256)
                .When(x => !string.IsNullOrWhiteSpace(x.Email));

            // A negative fee is a refund, not a price, and nothing downstream treats it as one.
            RuleFor(x => x.ConsultationFee)
                .GreaterThanOrEqualTo(0).WithMessage("The consultation fee cannot be negative.")
                .LessThanOrEqualTo(1_000_000).WithMessage("The consultation fee is unrealistically large.")
                .When(x => x.ConsultationFee.HasValue);

            RuleFor(x => x.Bio)
                .MaximumLength(2000).WithMessage("Bio must not exceed 2000 characters.");
        }
    }
}
