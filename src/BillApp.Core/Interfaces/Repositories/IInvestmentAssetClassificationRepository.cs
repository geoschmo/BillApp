using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Repositories;

public interface IInvestmentAssetClassificationRepository : IRepository<InvestmentAssetClassification>
{
    Task<IReadOnlyDictionary<string, string>> GetAssetClassesBySymbolAsync();

    Task UpsertAsync(string symbol, string assetClass);
}
