using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Services;

public interface IInvestmentImportService
{
    Task<InvestmentImportPreview> ParseTextAsync(string rawText, string accountName, string? sourceName = null);

    Task<InvestmentImportPreview> ParseCsvFileAsync(string filePath, string? sourceName = null);
}
