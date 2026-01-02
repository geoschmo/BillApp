using BillApp.Core.Enums;
using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for Account entities.
/// </summary>
public interface IAccountRepository : IRepository<Account>
{
    /// <summary>
    /// Gets all accounts of a specific type.
    /// </summary>
    Task<IEnumerable<Account>> GetByTypeAsync(AccountType type);

    /// <summary>
    /// Gets all active accounts.
    /// </summary>
    Task<IEnumerable<Account>> GetActiveAsync();

    /// <summary>
    /// Gets an account by name (case-insensitive).
    /// </summary>
    Task<Account?> GetByNameAsync(string name);
}
