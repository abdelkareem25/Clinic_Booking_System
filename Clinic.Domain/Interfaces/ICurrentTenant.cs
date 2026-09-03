namespace Clinic.Domain.Interfaces
{
    /// <summary>
    /// Which tenant the current request belongs to, expressed without any reference to HTTP.
    ///
    /// The exact counterpart of <see cref="ICurrentUser"/>, and named to match it: the persistence
    /// layer has to be able to ask "which clinic?" without reaching into HttpContext. The API
    /// supplies the implementation, which reads the tenant claim TokenService puts in the token.
    ///
    /// This is the ONLY place tenant identity enters the application. Nothing else - no controller,
    /// no repository, no specification - extracts it, which is what stops the extraction logic being
    /// copied, and then copied slightly wrong.
    /// </summary>
    public interface ICurrentTenant
    {
        /// <summary>
        /// The caller's tenant, or null when the request carries no tenant claim: an anonymous
        /// request, a background task, design-time tooling, or a test.
        ///
        /// Null means "see nothing", NOT "see everything". ClinicDbContext's query filter compares
        /// each row's TenantId against this value, and no row's TenantId is null, so a null tenant
        /// matches nothing: lists come back empty and lookups by id answer 404. That is the safe
        /// direction to fail - a request that somehow loses its claim produces a visibly broken
        /// feature rather than a silent leak of another clinic's records.
        /// </summary>
        int? TenantId { get; }
    }
}
