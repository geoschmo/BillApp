using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Infrastructure.Data;

namespace BillApp.Infrastructure.Repositories;

public class InvestmentAssetClassificationRepository
    : RepositoryBase<InvestmentAssetClassification>, IInvestmentAssetClassificationRepository
{
    public InvestmentAssetClassificationRepository(LiteDbContext context) : base(context)
    {
    }

    public Task<IReadOnlyDictionary<string, string>> GetAssetClassesBySymbolAsync()
    {
        var mappings = Collection
            .FindAll()
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Symbol) &&
                              !string.IsNullOrWhiteSpace(mapping.AssetClass))
            .GroupBy(mapping => NormalizeSymbol(mapping.Symbol))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(mapping => mapping.UpdatedAt ?? mapping.CreatedAt)
                    .First()
                    .AssetClass.Trim());

        return Task.FromResult<IReadOnlyDictionary<string, string>>(mappings);
    }

    public Task UpsertAsync(string symbol, string assetClass)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedAssetClass = assetClass.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSymbol) ||
            string.IsNullOrWhiteSpace(normalizedAssetClass))
        {
            return Task.CompletedTask;
        }

        var existing = Collection
            .FindAll()
            .FirstOrDefault(mapping => NormalizeSymbol(mapping.Symbol) == normalizedSymbol);
        if (existing == null)
        {
            Collection.Insert(new InvestmentAssetClassification
            {
                Symbol = normalizedSymbol,
                AssetClass = normalizedAssetClass
            });

            return Task.CompletedTask;
        }

        existing.AssetClass = normalizedAssetClass;
        existing.UpdatedAt = DateTime.UtcNow;
        Collection.Update(existing);
        return Task.CompletedTask;
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol.Trim().ToUpperInvariant();
    }
}
