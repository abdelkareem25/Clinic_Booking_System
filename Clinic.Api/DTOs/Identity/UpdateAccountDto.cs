namespace Clinic.Api.DTOs.Identity
{
    /// <summary>
    /// Edits to an existing account.
    ///
    /// The username is NOT editable. It is the subject of every issued token and the value audit
    /// stamps resolve through; renaming it would orphan the audit trail of everything that account
    /// has already done.
    /// </summary>
    public class UpdateAccountDto
    {
        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Optional administrative password reset. Null or empty leaves the password untouched -
        /// which is what an edit that only changes a phone number must do.
        /// </summary>
        public string? NewPassword { get; set; }

        public string? ConfirmNewPassword { get; set; }
    }
}
