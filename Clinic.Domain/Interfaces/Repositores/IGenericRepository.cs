using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Specifications;

namespace Clinic.Domain.Interfaces.Repository
{
    public interface IGenericRepository<T> where T : BaseEntity
    {
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> GetByIdAsync(int item);
        Task AddAsync(T item);
        Task DeleteAsync(T item);
        Task UpdateAsync(T item);
        Task<IReadOnlyList<T>> GetAllWithSpecAsync(ISpecification<T> spec);
        Task<T> GetByIdWithSpecAsync(ISpecification<T> spec);
        Task<T?> GetEntityWithSpec(ISpecification<T> spec);
        Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec);

    }
}
