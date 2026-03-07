using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Repositories;

public interface IInvestmentRepository : IRepository<InvestmentSnapshot>
{
    /// <summary>
    /// Returns all snapshots sorted descending by import time.
    /// </summary>
    Task<IEnumerable<InvestmentSnapshot>> GetAllOrderedAsync();

    /// <summary>
    /// Returns snapshots filtered by account name (case-insensitive).
    /// </summary>
    Task<IEnumerable<InvestmentSnapshot>> GetByAccountAsync(string accountName);
}
