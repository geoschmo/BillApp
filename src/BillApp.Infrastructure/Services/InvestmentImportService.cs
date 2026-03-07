using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;

namespace BillApp.Infrastructure.Services;

/// <summary>
/// Parses investment data from free-form text or structured CSV exports.
/// </summary>
public class InvestmentImportService : IInvestmentImportService
{
    private static readonly Regex PercentageRegex = new(@"(?<percent>\d+(?:\.\d+)?)%", RegexOptions.Compiled);
    private static readonly Regex CurrencyRegex = new(@"[+\-]?\$?\d[\d,\.]*", RegexOptions.Compiled);
    private static readonly Regex SymbolAfterHyphenRegex = new(@"-\s*(?<symbol>[A-Z0-9]{2,8})", RegexOptions.Compiled);
    private static readonly Regex SymbolStandaloneRegex = new(@"\b[A-Z]{3,6}\b", RegexOptions.Compiled);

    public Task<InvestmentImportPreview> ParseTextAsync(string rawText, string accountName, string? sourceName = null)
    {
        var preview = new InvestmentImportPreview
        {
            AccountName = accountName,
            SourceName = sourceName ?? "Text Import",
            ImportedAt = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(rawText))
        {
            preview.Warnings.Add("No text was provided.");
            return Task.FromResult(preview);
        }

        var lines = rawText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var blocks = new List<List<string>>();
        List<string>? currentBlock = null;

        foreach (var line in lines)
        {
            if (PercentageRegex.IsMatch(line))
            {
                if (currentBlock != null && currentBlock.Count > 0)
                {
                    blocks.Add(currentBlock);
                }

                currentBlock = new List<string> { line };
            }
            else if (currentBlock != null)
            {
                currentBlock.Add(line);
            }
        }

        if (currentBlock != null && currentBlock.Count > 0)
        {
            blocks.Add(currentBlock);
        }

        foreach (var block in blocks)
        {
            var holding = ParseTextBlock(string.Join(Environment.NewLine, block), accountName);

            if (holding == null)
            {
                preview.Warnings.Add($"Could not parse block starting with: {block.First()}");
                continue;
            }

            preview.Holdings.Add(holding);
        }

        if (preview.Holdings.Count == 0)
        {
            preview.Warnings.Add("No holdings were extracted from the text.");
        }

        return Task.FromResult(preview);
    }

    public Task<InvestmentImportPreview> ParseCsvFileAsync(string filePath, string? sourceName = null)
    {
        var preview = new InvestmentImportPreview
        {
            SourceName = sourceName ?? Path.GetFileName(filePath),
            ImportedAt = DateTime.UtcNow
        };

        if (!File.Exists(filePath))
        {
            preview.Warnings.Add("CSV file not found.");
            return Task.FromResult(preview);
        }

        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            preview.Warnings.Add("CSV file does not contain any data rows.");
            return Task.FromResult(preview);
        }

        var header = ParseCsvLine(lines[0]);
        var columnMap = BuildColumnMap(header);

        for (int i = 1; i < lines.Length; i++)
        {
            var row = ParseCsvLine(lines[i]);
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            var holding = ParseCsvRow(row, columnMap, i + 1);
            if (holding == null)
            {
                preview.Warnings.Add($"Skipping row {i + 1}: not enough data.");
                continue;
            }

            preview.Holdings.Add(holding);
        }

        if (preview.Holdings.Count == 0)
        {
            preview.Warnings.Add("No holdings were parsed from the CSV file.");
        }

        return Task.FromResult(preview);
    }

    private InvestmentHolding? ParseCsvRow(string[] row, Dictionary<string, int> columnMap, int rowNumber)
    {
        if (!TryGetValue(row, columnMap, "Account Name", out var accountName))
            return null;

        if (!TryGetValue(row, columnMap, "Current Value", out var currentValue))
            return null;

        var holding = new InvestmentHolding
        {
            AccountName = accountName,
            Symbol = GetValue(row, columnMap, "Symbol"),
            Description = GetValue(row, columnMap, "Description"),
            Quantity = ParseDecimal(GetValue(row, columnMap, "Quantity")) ?? 0m,
            Price = ParseDecimal(GetValue(row, columnMap, "Last Price")) ?? 0m,
            MarketValue = ParseDecimal(currentValue) ?? 0m,
            AssetClass = GetValue(row, columnMap, "Asset Class") ?? GetValue(row, columnMap, "Type"),
            PercentOfAccount = ParsePercent(GetValue(row, columnMap, "Percent Of Account")),
            SourceLine = $"Row {rowNumber}"
        };

        return holding;
    }

    private InvestmentHolding? ParseTextBlock(string block, string accountName)
    {
        var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        if (lines.Count == 0)
            return null;

        var holding = new InvestmentHolding
        {
            AccountName = accountName,
            SourceLine = block
        };

        holding.PercentOfAccount = ParsePercent(lines.First());
        holding.Description = ExtractDescription(lines);
        holding.Symbol = ExtractSymbol(holding.Description);
        holding.AssetClass = DetectAssetClass(lines);

        decimal? quantity = null;
        decimal? price = null;
        decimal? value = null;

        foreach (var line in lines.Skip(1))
        {
            foreach (Match match in CurrencyRegex.Matches(line))
            {
                var cleaned = match.Value.Replace("+", string.Empty).Trim();
                if (cleaned.Contains("%"))
                    continue;

                if (cleaned.Contains("$"))
                {
                    var parsed = ParseDecimal(cleaned);
                    if (parsed.HasValue)
                    {
                        if (price == null)
                        {
                            price = parsed.Value;
                        }
                        else if (value == null)
                        {
                            value = parsed.Value;
                        }
                    }
                }
                else if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                {
                    if (quantity == null)
                    {
                        quantity = number;
                    }
                }
            }
        }

        holding.Quantity = quantity ?? 0m;
        holding.Price = price ?? 0m;
        holding.MarketValue = value ?? 0m;

        return holding;
    }

    private static decimal? ParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = PercentageRegex.Match(value);
        if (match.Success && decimal.TryParse(match.Groups["percent"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var percent))
        {
            return percent;
        }

        return null;
    }

    private static string? ExtractDescription(List<string> lines)
    {
        return lines.Skip(1).FirstOrDefault(l => !Regex.IsMatch(l, @"^\d+%?$", RegexOptions.Compiled));
    }

    private static string? ExtractSymbol(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var match = SymbolAfterHyphenRegex.Match(description);
        if (match.Success)
        {
            return match.Groups["symbol"].Value.Trim();
        }

        match = SymbolStandaloneRegex.Match(description);
        if (match.Success)
        {
            return match.Value;
        }

        return null;
    }

    private static string? DetectAssetClass(IEnumerable<string> lines)
    {
        var joined = string.Join(" ", lines).ToLowerInvariant();
        if (joined.Contains("bond"))
            return "Bond";
        if (joined.Contains("cash"))
            return "Cash";
        if (joined.Contains("equity") || joined.Contains("stock"))
            return "Equity";
        if (joined.Contains("etf"))
            return "ETF";
        return null;
    }

    private Dictionary<string, int> BuildColumnMap(string[] header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++)
        {
            var columnName = header[i]?.Trim();
            if (!string.IsNullOrEmpty(columnName) && !map.ContainsKey(columnName))
            {
                map[columnName] = i;
            }
        }
        return map;
    }

    private static string? GetValue(string[] row, Dictionary<string, int> map, string columnName)
    {
        if (map.TryGetValue(columnName, out var index) && index < row.Length)
        {
            var value = row[index].Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        return null;
    }

    private static bool TryGetValue(string[] row, Dictionary<string, int> map, string columnName, out string value)
    {
        var raw = GetValue(row, map, columnName);
        if (raw != null)
        {
            value = raw;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = text.Replace("$", string.Empty)
            .Replace(",", string.Empty)
            .Replace("+", string.Empty)
            .Replace("(", "-")
            .Replace(")", string.Empty)
            .Trim();

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = string.Empty;
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    current += '\"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.Trim());
                current = string.Empty;
            }
            else
            {
                current += c;
            }
        }

        values.Add(current.Trim());
        return values.ToArray();
    }
}
