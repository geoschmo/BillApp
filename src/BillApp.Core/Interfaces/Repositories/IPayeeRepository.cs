using BillApp.Core.Enums;
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

    /// <summary>
    /// Gets all payees that are also accounts (IsAccount = true).
    /// </summary>
    Task<IEnumerable<Payee>> GetAccountsAsync();

    /// <summary>
    /// Gets all payees that can be used as payment methods (IsPaymentAccount = true).
    /// </summary>
    Task<IEnumerable<Payee>> GetPaymentAccountsAsync();

    /// <summary>
    /// Gets all accounts of a specific type.
    /// </summary>
    Task<IEnumerable<Payee>> GetAccountsByTypeAsync(AccountType type);

    /// <summary>
    /// Gets all active accounts (IsAccount = true AND IsActive = true).
    /// </summary>
    Task<IEnumerable<Payee>> GetActiveAccountsAsync();
}
