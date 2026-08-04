namespace Clinic.Api.DTOs.Identity
{
    /// <summary>
    /// A staff account as the administration screen renders it.
    ///
    /// Carries no password material of any kind - not the hash, not the salt, not the security
    /// stamp. A list endpoint that returns hashes turns one over-broad authorization mistake into an
    /// offline cracking exercise against every account at once.
    /// </summary>
    public class AccountDto
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>The login name. Today it is the email address; see AccountsController.Register.</summary>
        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        /// <summary>The single role this account holds, or empty when it has none.</summary>
        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        /// <summary>
        /// True while Identity's automatic lockout is in force. Distinct from <see cref="IsActive"/>,
        /// which is an administrator's decision - see AppUser.
        /// </summary>
        public bool IsLockedOut { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }
    }
}
