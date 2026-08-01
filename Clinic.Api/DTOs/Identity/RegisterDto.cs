using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.Identity
{
    public class RegisterDto
    {
        [Required]
        public string DisplayName { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, one special character, and be at least 8 characters long.")]
        public string Password { get; set; }
        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        /// <summary>
        /// Which role the new account gets. Optional; defaults to Patient, the least privileged.
        /// Validated against ClinicRoles in the controller - an unrecognised value has to be
        /// rejected rather than silently ignored, or the account ends up with no role at all and
        /// every authorised endpoint answers 403 for reasons nobody can see.
        /// </summary>
        public string? Role { get; set; }
    }
}
