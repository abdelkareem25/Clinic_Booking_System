using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Domain.Interfaces.Specifications;
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

        public async Task<IReadOnlyList<T>> GetAllWithSpecAsync(ISpecification<T> spec)
        {
            // db + specification pattern
            // call the specification evaluator to get the queryable with the specification applied
            return await ApplaySpecification(spec).ToListAsync();
        }

        public async Task<T> GetByIdWithSpecAsync(ISpecification<T> spec)
        {
            return await ApplaySpecification(spec).FirstOrDefaultAsync();
        }
        public Task<T?> GetEntityWithSpec(ISpecification<T> spec)
        {
            return ApplaySpecification(spec).FirstOrDefaultAsync();
        }

       public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec)
        {
            return await ApplaySpecification(spec).ToListAsync();
        }
        // becuse we need to write more clean code iam going to use private method to get the queryable with the specification applied

        private IQueryable<T> ApplaySpecification(ISpecification<T> spec)
        {
            return SpecificationEvalutor<T>.GetQuery(_context.Set<T>(), spec);
        }

        public async Task<int> CountAsync(ISpecification<T> spec)
        {
            return await ApplaySpecification(spec).CountAsync();
        }
    }
}
