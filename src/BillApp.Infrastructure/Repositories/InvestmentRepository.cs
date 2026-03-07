using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Infrastructure.Data;
using System.Linq;

namespace BillApp.Infrastructure.Repositories;

/// <summary>
/// LiteDB repository for investment snapshots.
/// </summary>
public class InvestmentRepository : RepositoryBase<InvestmentSnapshot>, IInvestmentRepository
{
    public InvestmentRepository(LiteDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<InvestmentSnapshot>> GetAllOrderedAsync()
    {
        var snapshots = Collection
            .FindAll()
            .OrderByDescending(s => s.ImportedAt);

        return Task.FromResult<IEnumerable<InvestmentSnapshot>>(snapshots);
    }

    public Task<IEnumerable<InvestmentSnapshot>> GetByAccountAsync(string accountName)
    {
        var lower = accountName.ToLowerInvariant();
        var snapshots = Collection
            .Find(s => s.Holdings.Any(h => h.AccountName.ToLowerInvariant() == lower))
            .OrderByDescending(s => s.ImportedAt);

        return Task.FromResult<IEnumerable<InvestmentSnapshot>>(snapshots);
    }
}
