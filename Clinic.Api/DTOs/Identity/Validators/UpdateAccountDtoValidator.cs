using FluentValidation;

namespace Clinic.Api.DTOs.Identity.Validators
{
    public class UpdateAccountDtoValidator : AbstractValidator<UpdateAccountDto>
    {
        public UpdateAccountDtoValidator()
        {
            RuleFor(x => x.DisplayName).ValidDisplayName();
            RuleFor(x => x.Email).ValidEmail();
            RuleFor(x => x.Role).KnownRole();

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(30)
                .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

            // The reset is opt-in: an edit that leaves the password blank must not be told its
            // blank password is too weak.
            RuleFor(x => x.NewPassword)
                .StrongPassword()
                .When(x => !string.IsNullOrEmpty(x.NewPassword));

            RuleFor(x => x.ConfirmNewPassword)
                .Equal(x => x.NewPassword).WithMessage("The passwords do not match.")
                .When(x => !string.IsNullOrEmpty(x.NewPassword));
        }
    }
}
