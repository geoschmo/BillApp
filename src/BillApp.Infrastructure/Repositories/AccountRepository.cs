using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Infrastructure.Data;

namespace BillApp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Account entities.
/// </summary>
public class AccountRepository : RepositoryBase<Account>, IAccountRepository
{
    public AccountRepository(LiteDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Account>> GetByTypeAsync(AccountType type)
    {
        var accounts = Collection.Find(a => a.AccountType == type);
        return Task.FromResult(accounts);
    }

    public Task<IEnumerable<Account>> GetActiveAsync()
    {
        var accounts = Collection.Find(a => a.IsActive);
        return Task.FromResult(accounts);
    }

    public Task<Account?> GetByNameAsync(string name)
    {
        var account = Collection.FindOne(a =>
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<Account?>(account);
    }
}
