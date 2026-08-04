using FluentValidation;

namespace Clinic.Api.DTOs.Identity.Validators
{
    public class CreateAccountDtoValidator : AbstractValidator<CreateAccountDto>
    {
        public CreateAccountDtoValidator()
        {
            RuleFor(x => x.DisplayName).ValidDisplayName();
            RuleFor(x => x.Email).ValidEmail();
            RuleFor(x => x.Role).KnownRole();

            RuleFor(x => x.UserName)
                .MaximumLength(256)
                // Identity's default allowed set. Rejecting here gives the offending field a name;
                // letting Identity reject it produces a bare message with no field to attach it to.
                .Matches(@"^[a-zA-Z0-9@._\-+]+$")
                    .WithMessage("The username may contain only letters, digits and @ . _ - +")
                .When(x => !string.IsNullOrWhiteSpace(x.UserName));

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(30)
                .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("A password is required.")
                .StrongPassword();

            // Uniqueness of the email and username is NOT checked here. It cannot be: a validator
            // has no database, and even with one the check would be a race - two concurrent requests
            // both pass, both insert, and the unique index decides. The controller owns that, where
            // the failure can be answered with a 409.
            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password).WithMessage("The passwords do not match.");
        }
    }
}
