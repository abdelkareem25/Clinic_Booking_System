using AutoMapper;
using Clinic.Domain.Entites;

namespace Clinic.Api.Helper
{
    public static class AuditMappingExtensions
    {
        /// <summary>
        /// Excludes the key, the concurrency token and the audit columns from any request DTO to
        /// entity map.
        ///
        /// This is not merely bookkeeping to satisfy AssertConfigurationIsValid. These members are
        /// owned by the persistence layer and by ClinicDbContext.StampAuditColumns; letting a
        /// request payload set them would let a caller forge who created a record, backdate it, or
        /// supply a stale concurrency token and defeat the very check it exists for.
        ///
        /// One helper rather than five ForMember calls per map, so adding a future audit column is
        /// a single edit instead of six.
        /// </summary>
        public static IMappingExpression<TSource, TDestination> IgnoreSystemOwnedMembers<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> map)
            where TDestination : BaseEntity
        {
            // MULTI-TENANT: TenantId is system-owned in exactly the same sense as the audit
            // columns, and more sharply so. A request payload able to set it would be a
            // cross-tenant WRITE - a caller planting a record inside someone else's clinic, or
            // moving one of ours into theirs - which no amount of read filtering would catch.
            // Ignored here so the guarantee is one edit covering every request map, present and
            // future, rather than a ForMember someone has to remember on the next DTO.
            //
            // Matched by name rather than by a typed lambda so the constraint can stay at
            // BaseEntity: Tenant is itself a BaseEntity but NOT an ITenantEntity, and tightening
            // the constraint to ITenantEntity would lock the tenant-creation map out of this
            // helper. MappingProfileTests.Configuration_Is_Valid fails loudly if the name is ever
            // wrong, so the lost compile-time check is covered.
            if (typeof(ITenantEntity).IsAssignableFrom(typeof(TDestination)))
            {
                map.ForMember(nameof(ITenantEntity.TenantId), options => options.Ignore());
            }

            return map
                .ForMember(destination => destination.Id, options => options.Ignore())
                .ForMember(destination => destination.RowVersion, options => options.Ignore())
                .ForMember(destination => destination.CreatedAtUtc, options => options.Ignore())
                .ForMember(destination => destination.CreatedBy, options => options.Ignore())
                .ForMember(destination => destination.ModifiedAtUtc, options => options.Ignore())
                .ForMember(destination => destination.ModifiedBy, options => options.Ignore());
        }
    }
}
