using Clinic.Domain.Entites;
using Clinic.Domain.Interfaces;
using Clinic.Domain.Interfaces.Repository;
using Clinic.Infrastructure.Data.Context;
using System.Collections;

namespace Clinic.Infrastructure.Repositores
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ClinicDbContext _db;
        private Hashtable _repositories;

        public UnitOfWork(ClinicDbContext db)
        {
            _db = db;
            _repositories = new Hashtable();
        }
        public async Task<int> CompleteAsync() => await _db.SaveChangesAsync();

        public ValueTask DisposeAsync() => _db.DisposeAsync();

        public IGenericRepository<T> Repository<T>() where T : BaseEntity
        {
            var type = typeof(T).Name; // Key
            if(!_repositories.ContainsKey(type)) // Key :Value Pair
            {
                 var Repository = new GenericRepository<T>(_db);
                _repositories.Add(type, Repository);
            }
            //We need to store this object in collection cuz if we call it again he return the same object
            
            return _repositories[type] as IGenericRepository<T>;
        }
    }
}
