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
