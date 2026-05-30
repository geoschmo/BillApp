using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BillApp.ViewModels;

public partial class InvestmentsViewModel : ViewModelBase
{
    private readonly IInvestmentImportService _importService;
    private readonly IInvestmentRepository _investmentRepository;
    private readonly IInvestmentAssetClassificationRepository _assetClassificationRepository;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _accountName = "Investments";

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private string? _selectedCsvPath;

    [ObservableProperty]
    private string _sourceName = "Text Import";

    [ObservableProperty]
    private DateTime? _effectiveDate = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<InvestmentHoldingRow> _previewHoldings = new();

    [ObservableProperty]
    private ObservableCollection<string> _warnings = new();

    [ObservableProperty]
    private bool _isParsing;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private decimal _totalValue;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public IReadOnlyList<string> AssetClassOptions { get; } =
    [
        "Equity",
        "Bond",
        "Cash",
        "ETF",
        "Mutual Fund",
        "Money Market",
        "Real Estate",
        "Commodity",
        "Other"
    ];

    public InvestmentsViewModel(
        IInvestmentImportService importService,
        IInvestmentRepository investmentRepository,
        IInvestmentAssetClassificationRepository assetClassificationRepository,
        INavigationService navigationService)
    {
        _importService = importService;
        _investmentRepository = investmentRepository;
        _assetClassificationRepository = assetClassificationRepository;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task ParseTextAsync()
    {
        if (string.IsNullOrWhiteSpace(AccountName) || string.IsNullOrWhiteSpace(RawText))
        {
            StatusMessage = "Please provide an account name and pasted text.";
            return;
        }

        IsParsing = true;

        try
        {
            var preview = await _importService.ParseTextAsync(RawText, AccountName, SourceName);
            ApplyPreview(preview);
            await ApplyKnownAssetClassesAsync();
            SelectedCsvPath = null;
            StatusMessage = $"Parsed {preview.Holdings.Count} holding(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error parsing text: {ex.Message}";
        }
        finally
        {
            IsParsing = false;
        }
    }

    [RelayCommand]
    private async Task LoadCsvAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Investment CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() != true)
            return;

        SelectedCsvPath = dialog.FileName;
        SourceName = Path.GetFileName(dialog.FileName);
        IsParsing = true;

        try
        {
            var preview = await _importService.ParseCsvFileAsync(dialog.FileName, SourceName);
            ApplyPreview(preview);
            await ApplyKnownAssetClassesAsync();
            StatusMessage = preview.Warnings.Any()
                ? $"Parsed {preview.Holdings.Count} holding(s) with warnings."
                : $"Parsed {preview.Holdings.Count} holding(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading CSV: {ex.Message}";
        }
        finally
        {
            IsParsing = false;
        }
    }

    [RelayCommand]
    private async Task CommitImportAsync()
    {
        if (!PreviewHoldings.Any())
        {
            StatusMessage = "No holdings to import.";
            return;
        }

        IsImporting = true;

        try
        {
            var snapshot = new InvestmentSnapshot
            {
                EffectiveDate = EffectiveDate?.Date ?? DateTime.Today,
                ImportedAt = DateTime.UtcNow,
                SourceName = SourceName,
                Notes = $"Imported {PreviewHoldings.Count} holding(s) from {SourceName}",
                Holdings = PreviewHoldings.Select(h => h.ToDomain()).ToList()
            };

            await _investmentRepository.InsertAsync(snapshot);
            await SaveAssetClassMappingsAsync();
            StatusMessage = $"Saved {snapshot.Holdings.Count} holding(s).";
            ClearPreview();
            await ReturnToOverviewAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void ApplyPreview(InvestmentImportPreview preview)
    {
        PreviewHoldings.Clear();
        foreach (var holding in preview.Holdings)
        {
            PreviewHoldings.Add(new InvestmentHoldingRow(holding));
        }

        Warnings.Clear();
        foreach (var warning in preview.Warnings)
        {
            Warnings.Add(warning);
        }

        TotalValue = preview.TotalValue;
        SourceName = preview.SourceName ?? SourceName;
    }

    private async Task ApplyKnownAssetClassesAsync()
    {
        var assetClassesBySymbol = await _assetClassificationRepository.GetAssetClassesBySymbolAsync();
        foreach (var holding in PreviewHoldings)
        {
            if (string.IsNullOrWhiteSpace(holding.Symbol))
            {
                continue;
            }

            if (assetClassesBySymbol.TryGetValue(NormalizeSymbol(holding.Symbol), out var assetClass))
            {
                holding.AssetClass = assetClass;
            }
        }
    }

    private async Task SaveAssetClassMappingsAsync()
    {
        var mappings = PreviewHoldings
            .Where(holding => !string.IsNullOrWhiteSpace(holding.Symbol) &&
                              !string.IsNullOrWhiteSpace(holding.AssetClass))
            .GroupBy(holding => NormalizeSymbol(holding.Symbol!))
            .Select(group => group.First());

        foreach (var mapping in mappings)
        {
            await _assetClassificationRepository.UpsertAsync(mapping.Symbol!, mapping.AssetClass!);
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol.Trim().ToUpperInvariant();
    }

    private void ClearPreview()
    {
        PreviewHoldings.Clear();
        Warnings.Clear();
        TotalValue = 0m;
        RawText = string.Empty;
        SelectedCsvPath = null;
    }

    private async Task ReturnToOverviewAsync()
    {
        if (_navigationService.CanGoBack)
        {
            await _navigationService.GoBackAsync();
            return;
        }

        await _navigationService.NavigateToAsync<InvestmentsOverviewViewModel>();
    }
}
