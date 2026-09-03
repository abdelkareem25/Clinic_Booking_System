using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;

namespace Clinic.Tests.TestSupport
{
    /// <summary>
    /// A settable <see cref="ICurrentTenant"/> so tests can choose which clinic is acting, and
    /// change it mid-test - which is exactly what an isolation test has to do: seed as tenant A,
    /// then read as tenant B and prove nothing comes back.
    ///
    /// Mirrors <see cref="StubCurrentUser"/>. Defaults to <see cref="Tenant.DefaultTenantId"/>
    /// rather than to null, because null means "see nothing": a test that forgot to set a tenant
    /// would otherwise pass trivially against an empty result set, which is the one way an
    /// isolation test can look green while proving nothing at all.
    /// </summary>
    public sealed class StubCurrentTenant : ICurrentTenant
    {
        public StubCurrentTenant(int? tenantId = Tenant.DefaultTenantId) => TenantId = tenantId;

        public int? TenantId { get; set; }
    }
}
