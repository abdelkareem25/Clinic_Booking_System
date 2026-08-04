namespace Clinic.Api.DTOs.Identity
{
    /// <summary>
    /// Provisioning payload for a staff account. Validated by CreateAccountDtoValidator.
    ///
    /// Data annotations are deliberately absent: FluentValidation owns this contract, and two
    /// validation systems on one DTO produce two different error shapes for the same field.
    /// </summary>
    public class CreateAccountDto
    {
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Optional. The email IS the username in this system (see AccountsController.Register), so
        /// when this is omitted the email is used. Accepting it explicitly keeps the client's form
        /// honest about what it is setting.
        /// </summary>
        public string? UserName { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
