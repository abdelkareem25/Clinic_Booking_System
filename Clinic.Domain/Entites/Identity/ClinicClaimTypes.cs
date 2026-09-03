namespace Clinic.Domain.Entites.Identity
{
    /// <summary>
    /// The claim types this system defines, beyond the ones System.Security.Claims already names.
    ///
    /// Const, and shared, for the same reason <see cref="ClinicRoles"/> is: this claim is WRITTEN in
    /// one project (TokenService, in Clinic.Application) and READ in another
    /// (HttpContextCurrentTenant, in Clinic.Api). A string literal duplicated across that boundary
    /// is one typo away from a claim that is issued and never found - and that failure is silent.
    /// It does not throw and it does not log; it simply means the tenant resolves to null, and null
    /// means "see nothing". The symptom is a user who signs in successfully and finds an empty
    /// application, which is about the hardest thing there is to trace back to a misspelling.
    ///
    /// A short custom name rather than a schema URI: it is read by this application only, and every
    /// character of a claim name is a character in every token this API issues.
    /// </summary>
    public static class ClinicClaimTypes
    {
        /// <summary>The tenant the authenticated account belongs to. Absent when it belongs to none.</summary>
        public const string TenantId = "tenantId";
    }
}
