namespace Clinic.Api.DTOs.Identity
{
    /// <summary>
    /// The result of provisioning an account. Deliberately NOT UserDto.
    ///
    /// UserDto carries a Token, and registration used to return one. That was already questionable
    /// when registration was self-service; now that an administrator provisions accounts for other
    /// people, it would hand the administrator a bearer token that impersonates the new user. The
    /// new user authenticates for themselves through Login.
    /// </summary>
    public class RegisteredUserDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
