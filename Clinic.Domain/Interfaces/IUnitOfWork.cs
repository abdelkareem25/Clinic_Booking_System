using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Repository;

namespace Clinic.Domain.Interfaces
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        Task<int> CompleteAsync();
        IGenericRepository<T> Repository<T>() where T : BaseEntity;
    }
}
