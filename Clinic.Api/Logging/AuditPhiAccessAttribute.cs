namespace Clinic.Api.Logging
{
    /// <summary>
    /// Marks a controller or action as touching protected health information.
    ///
    /// A clinical system is expected to be able to answer "who looked at this patient's record, and
    /// when" - and today this application cannot answer it at all. Marking the endpoints explicitly
    /// rather than inferring it keeps the decision visible in the controller and greppable in the
    /// codebase; PhiAuditCoverageTests asserts nothing clinical is left unmarked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AuditPhiAccessAttribute : Attribute
    {
        public AuditPhiAccessAttribute(string resourceType)
        {
            ResourceType = resourceType;
        }

        /// <summary>The kind of record being touched, e.g. "Patient" or "Appointment".</summary>
        public string ResourceType { get; }
    }
}
