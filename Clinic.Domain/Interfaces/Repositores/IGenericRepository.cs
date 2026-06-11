using Clinic.Domain.Entites;

namespace Clinic.Domain.Interfaces.Repository
{
    public interface IGenericRepository<T> where T : BaseEntity
    {
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> GetByIdAsync(int item);
        Task AddAsync(T item);
        Task DeleteAsync(T item);
        Task UpdateAsync(T item);

    }
}
