using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Repositores
{
    public static class SpecificationEvalutor<T> where T : BaseEntity
    {
        public static IQueryable<T> GetQuery(IQueryable<T> inputquery , ISpecification<T> spec)
        {
            var query = inputquery;
            if (spec.Criteria != null)
            {
                query = query.Where(spec.Criteria);
            }
            if(spec.Includes!= null)
            {
                query = spec.Includes.Aggregate(query, (current, includeExpression) => current.Include(includeExpression));
            }
            return query;
        }
    }
}
