using System.ComponentModel.DataAnnotations;

namespace Clinic.Api.DTOs.Identity
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, one special character, and be at least 8 characters long.")]
        public string Password { get; set; }
    }
}
