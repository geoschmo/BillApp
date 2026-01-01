using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for Category entities.
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetByNameAsync(string name);
    Task<IEnumerable<Category>> GetDefaultCategoriesAsync();
}
