using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Repositores
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        private readonly ClinicDbContext _context;

        public GenericRepository(ClinicDbContext context)
        {
            _context = context;
        }
        public async Task<IReadOnlyList<T>> GetAllAsync()
            => await _context.Set<T>().ToListAsync();
        

        public async Task<T> GetByIdAsync(int item)
            => await _context.Set<T>().FindAsync(item);
        

        public async Task AddAsync(T item)
            =>await _context.Set<T>().AddAsync(item);

        public async Task UpdateAsync(T item)
            => _context.Set<T>().Update(item);

        public async Task DeleteAsync(T item)
            => _context.Set<T>().Remove(item);
        
    }
}
