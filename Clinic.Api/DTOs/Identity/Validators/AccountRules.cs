using Clinic.Domain.Entites.Identity;
using FluentValidation;

namespace Clinic.Api.DTOs.Identity.Validators
{
    /// <summary>
    /// Rules shared by account creation and account editing.
    ///
    /// The password rule in particular must be stated once. It has to agree with the Identity
    /// password options configured at startup, and two copies of "8+ with upper, lower, digit and
    /// symbol" drift the moment one of them is relaxed - at which point the API rejects a password
    /// Identity would have accepted, or worse, accepts one Identity then rejects with an error the
    /// client cannot map to a field.
    /// </summary>
    internal static class AccountRules
    {
        public const int PasswordMinimumLength = 8;

        public static IRuleBuilderOptions<T, string?> StrongPassword<T>(
            this IRuleBuilder<T, string?> rule)
        {
            return rule
                .MinimumLength(PasswordMinimumLength)
                    .WithMessage($"The password must be at least {PasswordMinimumLength} characters long.")
                .Matches("[A-Z]").WithMessage("The password must contain an uppercase letter.")
                .Matches("[a-z]").WithMessage("The password must contain a lowercase letter.")
                .Matches(@"\d").WithMessage("The password must contain a digit.")
                .Matches(@"[^A-Za-z0-9]").WithMessage("The password must contain a symbol.");
        }

        public static IRuleBuilderOptions<T, string> KnownRole<T>(this IRuleBuilder<T, string> rule)
        {
            return rule
                .NotEmpty().WithMessage("A role is required.")
                .Must(role => ClinicRoles.All.Contains(role, StringComparer.Ordinal))
                .WithMessage($"Unknown role. Valid roles are: {string.Join(", ", ClinicRoles.All)}.");
        }

        public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule)
        {
            return rule
                .NotEmpty().WithMessage("An email address is required.")
                .EmailAddress().WithMessage("Enter a valid email address.")
                .MaximumLength(256);
        }

        public static IRuleBuilderOptions<T, string> ValidDisplayName<T>(this IRuleBuilder<T, string> rule)
        {
            return rule
                .NotEmpty().WithMessage("A full name is required.")
                .MaximumLength(100).WithMessage("The full name must not exceed 100 characters.");
        }
    }
}
