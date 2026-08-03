using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Repositores
{
    public static class SpecificationEvalutor<T> where T : BaseEntity
    {
        /// <summary>
        /// Composes a specification into a query, in the order the operators have to be applied.
        ///
        /// The previous version applied them as: filter, order by, order by descending, page,
        /// include. Three problems with that:
        ///
        ///   1. Two sequential `if`s meant a specification carrying both an ascending and a
        ///      descending key applied OrderBy and then OrderByDescending. That is not a secondary
        ///      sort - OrderByDescending REPLACES the primary ordering, so the first key was
        ///      silently discarded. No specification set both, so it sat waiting for the first one
        ///      that did.
        ///
        ///   2. Nothing broke ties. Ordering by a non-unique column - DayOfWeek, Name - leaves the
        ///      database free to return equal rows in any order it likes, and it need not choose the
        ///      same order twice. Under OFFSET/FETCH that means a row can appear on two consecutive
        ///      pages, or on none at all, while the total count looks perfectly correct.
        ///
        ///   3. Includes were attached after Skip/Take. EF Core tolerates it, but composing the
        ///      shape of the result after slicing it is backwards and depends on the provider
        ///      hoisting the Include for you.
        /// </summary>
        public static IQueryable<T> GetQuery(IQueryable<T> inputquery , ISpecification<T> spec)
        {
            var query = inputquery;

            // 1. Filter first: everything downstream then works on the smallest possible set.
            if (spec.Criteria != null)
            {
                query = query.Where(spec.Criteria);
            }

            // 2. Shape the results before slicing them.
            query = spec.Includes.Aggregate(query, (current, includeExpression) => current.Include(includeExpression));

            if (spec.AsSplitQuery)
            {
                query = query.AsSplitQuery();
            }

            // 3. Order, always ending with a unique key.
            //
            // if/else, not two ifs: the sort keys are alternatives, not a sequence.
            IOrderedQueryable<T> ordered =
                spec.OrderBy is not null ? query.OrderBy(spec.OrderBy)
                : spec.OrderByDescending is not null ? query.OrderByDescending(spec.OrderByDescending)
                : query.OrderBy(entity => entity.Id);

            // Id is the primary key, so appending it makes the ordering total: equal values in the
            // chosen column are now separated deterministically and paging is stable. Harmless when
            // the chosen column is already unique - the tie-break never fires.
            query = ordered.ThenBy(entity => entity.Id);

            // 4. Page last, over a fully determined ordering.
            if (spec.IsPaginationEnable)
            {
                query = query.Skip(spec.Skip).Take(spec.Take);
            }

            return query;
        }

        /// <summary>
        /// Builds the query behind CountAsync: the specification's filter and nothing else.
        ///
        /// Pagination is deliberately ignored. Counting a single page is meaningless - that number
        /// is already Data.Count - and applying Skip/Take before COUNT(*) clamps the total to
        /// PageSize, which makes a paginated client believe there is only ever one page. Passing a
        /// paginated specification to CountAsync was a real defect in DoctorsController, and it is
        /// the kind of mistake that hides in plain sight because both specifications are valid
        /// objects of the same type. Handling it here makes the whole class of bug unreachable.
        ///
        /// Ordering and includes are dropped too: neither changes a count, and both make the
        /// database do work - a sort and a set of joins - whose results are then discarded.
        /// </summary>
        public static IQueryable<T> GetCountQuery(IQueryable<T> inputquery, ISpecification<T> spec)
        {
            return spec.Criteria is null ? inputquery : inputquery.Where(spec.Criteria);
        }
    }
}
