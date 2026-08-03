namespace Clinic.Domain.Interfaces
{
    /// <summary>
    /// Who is making the current request, expressed without any reference to HTTP.
    ///
    /// The audit columns need to record an actor, and the persistence layer must not reach into
    /// HttpContext to find one. The API supplies the implementation.
    /// </summary>
    public interface ICurrentUser
    {
        /// <summary>The authenticated user's identifier, or null for unauthenticated or background work.</summary>
        string? UserId { get; }
    }
}
