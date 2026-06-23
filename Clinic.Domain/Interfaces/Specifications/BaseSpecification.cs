using Clinic.Domain.Entites;
using System.Linq.Expressions;

namespace Clinic.Domain.Interfaces.Specifications
{
    public class BaseSpecification<T> : ISpecification<T> where T : BaseEntity
    {
        public Expression<Func<T, bool>>? Criteria { get; set; }
        public List<Expression<Func<T, object>>> Includes { get; set; } = new List<Expression<Func<T, object>>>();
        public int Take { get; set; }
        public int Skip { get; set; }
        public bool IsPaginationEnable { get; set; }
        public Expression<Func<T, object>>? OrderBy { get; set; }
        public Expression<Func<T, object>>? OrderByDescending { get; set; }

        //get all
        public BaseSpecification()
        {
             
        }
        public BaseSpecification(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }

        public void AddOrderBy(Expression<Func<T, object>> orderBy)
        {
            OrderBy = orderBy;
        }
        public void AddOrderByDescending(Expression<Func<T,object >> orderByDescending)
        {
            OrderByDescending = orderByDescending;
        }
        public void ApplyPagination(int skip , int take)
        {
            Take = take;
            Skip = skip;
            IsPaginationEnable = true;
        }
    }
}
