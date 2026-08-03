using Clinic.Domain.Interfaces;

namespace Clinic.Tests.TestSupport
{
    /// <summary>A settable <see cref="ICurrentUser"/> so tests can change who is acting mid-test.</summary>
    public sealed class StubCurrentUser : ICurrentUser
    {
        public StubCurrentUser(string? userId = null) => UserId = userId;

        public string? UserId { get; set; }
    }
}
