using System.ComponentModel.DataAnnotations;

namespace Clinic.Application
{
    /// <summary>
    /// Strongly-typed binding for the "JWT" configuration section.
    ///
    /// Both halves of the authentication system read these values: <see cref="TokenService"/> signs
    /// with them and the JWT bearer handler validates against them. Sharing one options object is
    /// what guarantees the two agree - previously the token was signed with JWT:Key while the
    /// handler had no key configured at all, so every token was rejected.
    /// </summary>
    public sealed class JwtOptions
    {
        public const string SectionName = "JWT";

        /// <summary>
        /// HMAC-SHA256 signing key. RFC 7518 requires a key at least as long as the hash output
        /// (256 bits / 32 bytes); shorter keys make the signature trivially brute-forceable.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "JWT:Key is required.")]
        [MinLength(32, ErrorMessage = "JWT:Key must be at least 32 characters (256 bits) for HMAC-SHA256.")]
        public string Key { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "JWT:Issuer is required.")]
        public string Issuer { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "JWT:Audience is required.")]
        public string Audience { get; set; } = string.Empty;

        [Range(1, 30, ErrorMessage = "JWT:ExpireInDays must be between 1 and 30.")]
        public int ExpireInDays { get; set; } = 1;
    }
}
