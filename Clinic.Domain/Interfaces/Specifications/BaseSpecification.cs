using Clinic.Domain.Entites;
using System.Linq.Expressions;

namespace Clinic.Domain.Interfaces.Specifications
{
    public class BaseSpecification<T> : ISpecification<T> where T : BaseEntity
    {
        private readonly List<Expression<Func<T, object>>> _includes = new();

        public Expression<Func<T, bool>>? Criteria { get; set; }
        public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;
        public int Take { get; set; }
        public int Skip { get; set; }
        public bool IsPaginationEnable { get; set; }
        public Expression<Func<T, object>>? OrderBy { get; private set; }
        public Expression<Func<T, object>>? OrderByDescending { get; private set; }
        public bool AsSplitQuery { get; protected set; }

        //get all
        public BaseSpecification()
        {

        }
        public BaseSpecification(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }

        /// <summary>
        /// Registers a navigation property for eager loading.
        ///
        /// The expression MUST be a navigation property (an entity or a collection of entities).
        /// Because the list is typed as Expression&lt;Func&lt;T, object&gt;&gt;, a scalar member such
        /// as a => a.Doctor.Name also compiles - string boxes to object - but EF Core rejects it at
        /// query time with "The expression ... is invalid inside an 'Include' operation". Adding
        /// includes only through this method keeps every call site in one place; the
        /// SpecificationIncludeTests guard verifies none of them is a scalar.
        /// </summary>
        protected void AddInclude(Expression<Func<T, object>> includeExpression)
        {
            _includes.Add(includeExpression);
        }

        /// <summary>
        /// Sorts ascending. Clears any descending sort: a specification holding both was silently
        /// ambiguous, and the evaluator resolved it by letting one overwrite the other.
        /// </summary>
        public void AddOrderBy(Expression<Func<T, object>> orderBy)
        {
            OrderBy = orderBy;
            OrderByDescending = null;
        }

        /// <summary>Sorts descending. Clears any ascending sort - see <see cref="AddOrderBy"/>.</summary>
        public void AddOrderByDescending(Expression<Func<T,object >> orderByDescending)
        {
            OrderByDescending = orderByDescending;
            OrderBy = null;
        }

        /// <summary>
        /// Splits a query with multiple collection includes into one round trip per collection.
        ///
        /// A single query joining two collections returns the cartesian product of both: 50
        /// appointments and 50 schedules for one doctor become 2,500 rows on the wire, of which EF
        /// discards all but 100. Worth turning on for any specification that includes more than one
        /// collection.
        /// </summary>
        protected void UseSplitQuery() => AsSplitQuery = true;

        public void ApplyPagination(int skip , int take)
        {
            Take = take;
            Skip = skip;
            IsPaginationEnable = true;
        }
    }
}
