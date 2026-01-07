using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for Payee operations.
/// </summary>
public interface IPayeeRepository : IRepository<Payee>
{
    /// <summary>
    /// Finds a payee by name (case-insensitive).
    /// </summary>
    Task<Payee?> GetByNameAsync(string name);

    /// <summary>
    /// Gets all payees with their associated categories loaded.
    /// </summary>
    Task<IEnumerable<Payee>> GetAllWithCategoriesAsync();
}
