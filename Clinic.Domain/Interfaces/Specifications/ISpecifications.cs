using Clinic.Domain.Entites;
using System.Linq.Expressions;

namespace Clinic.Domain.Interfaces.Specifications
{
    public interface ISpecification<T> where T : BaseEntity
    {
        public Expression<Func<T, bool>>? Criteria { get; set; }

        /// <summary>
        /// Navigation properties to eager-load. Read-only: includes are added through
        /// <see cref="BaseSpecification{T}.AddInclude"/> so a specification owns its own list and
        /// nothing outside can replace or mutate it.
        /// </summary>
        public IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

        /// <summary>
        /// Ascending sort key, if any. Mutually exclusive with <see cref="OrderByDescending"/> -
        /// the setters on BaseSpecification clear one another.
        /// </summary>
        public Expression<Func<T,object>>? OrderBy { get; }

        public Expression<Func<T,object>>? OrderByDescending { get; }

        /// <summary>Issue one query per included collection instead of a single joined query.</summary>
        public bool AsSplitQuery { get; }

        public int Take { get; set; }
        public int Skip { get; set; }
        public bool IsPaginationEnable { get; set; }
    }
}
