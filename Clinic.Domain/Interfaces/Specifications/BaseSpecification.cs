using Clinic.Domain.Entites;
using System.Linq.Expressions;

namespace Clinic.Domain.Interfaces.Specifications
{
    public class BaseSpecification<T> : ISpecification<T> where T : BaseEntity
    {
        public Expression<Func<T, bool>> Criteria { get; set; }
        public List<Expression<Func<T, object>>> Includes { get; set; } = new List<Expression<Func<T, object>>>();

        //get all
        public BaseSpecification()
        {

        }
        public BaseSpecification(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }
    }
}
