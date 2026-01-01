using System.Linq.Expressions;
using BillApp.Core.Interfaces;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Infrastructure.Data;
using LiteDB;

namespace BillApp.Infrastructure.Repositories;

/// <summary>
/// Base repository implementation using LiteDB.
/// Provides common CRUD operations for all entities.
/// </summary>
public class RepositoryBase<T> : IRepository<T> where T : class, IEntity
{
    protected readonly LiteDbContext Context;

    /// <summary>
    /// Gets the LiteDB collection for this entity type.
    /// </summary>
    protected ILiteCollection<T> Collection => Context.GetCollection<T>();

    public RepositoryBase(LiteDbContext context)
    {
        Context = context;
    }

    public virtual Task<T?> GetByIdAsync(Guid id)
    {
        var entity = Collection.FindById(id);
        return Task.FromResult<T?>(entity);
    }

    public virtual Task<IEnumerable<T>> GetAllAsync()
    {
        var entities = Collection.FindAll();
        return Task.FromResult(entities);
    }

    public virtual Task<Guid> InsertAsync(T entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        Collection.Insert(entity);
        return Task.FromResult(entity.Id);
    }

    public virtual Task UpdateAsync(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        Collection.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(Guid id)
    {
        Collection.Delete(id);
        return Task.CompletedTask;
    }

    public virtual Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        var entities = Collection.Find(predicate);
        return Task.FromResult(entities);
    }

    public virtual Task<int> CountAsync()
    {
        var count = Collection.Count();
        return Task.FromResult(count);
    }

    public virtual Task<bool> ExistsAsync(Guid id)
    {
        var exists = Collection.Exists(x => x.Id == id);
        return Task.FromResult(exists);
    }
}
