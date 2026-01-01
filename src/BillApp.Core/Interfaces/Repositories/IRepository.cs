using System.Linq.Expressions;

namespace BillApp.Core.Interfaces.Repositories;

/// <summary>
/// Generic repository interface for CRUD operations.
/// Similar to Angular services that interact with backend APIs.
/// </summary>
public interface IRepository<T> where T : class, IEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<Guid> InsertAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<int> CountAsync();
    Task<bool> ExistsAsync(Guid id);
}
