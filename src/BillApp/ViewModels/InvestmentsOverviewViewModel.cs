using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class InvestmentsOverviewViewModel : ViewModelBase
{
    private readonly IInvestmentRepository _investmentRepository;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<InvestmentAccountSummaryRow> _accountSummaries = new();

    [ObservableProperty]
    private InvestmentAccountSummaryRow? _selectedAccount;

    [ObservableProperty]
    private ObservableCollection<InvestmentAccountHistoryRow> _accountHistory = new();

    [ObservableProperty]
    private ObservableCollection<InvestmentHoldingRow> _currentAccountHoldings = new();

    [ObservableProperty]
    private decimal _totalCurrentValue;

    [ObservableProperty]
    private ObservableCollection<InvestmentAssetAllocationRow> _assetAllocations = new();

    [ObservableProperty]
    private ObservableCollection<InvestmentSnapshot> _snapshots = new();

    [ObservableProperty]
    private InvestmentSnapshot? _selectedSnapshot;

    [ObservableProperty]
    private ObservableCollection<InvestmentHoldingRow> _snapshotHoldings = new();

    [ObservableProperty]
    private decimal _selectedSnapshotTotal;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _newAccountName = string.Empty;

    [ObservableProperty]
    private string _newSymbol = string.Empty;

    [ObservableProperty]
    private string _newDescription = string.Empty;

    [ObservableProperty]
    private decimal _newQuantity;

    [ObservableProperty]
    private decimal _newPrice;

    [ObservableProperty]
    private decimal _newMarketValue;

    [ObservableProperty]
    private string _newAssetClass = string.Empty;

    [ObservableProperty]
    private decimal? _newPercentOfAccount;

    public InvestmentsOverviewViewModel(
        IInvestmentRepository investmentRepository,
        INavigationService navigationService)
    {
        _investmentRepository = investmentRepository;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        await LoadSnapshotsAsync();
    }

    [RelayCommand]
    public async Task LoadSnapshotsAsync()
    {
        IsLoading = true;
        try
        {
            var snapshots = await _investmentRepository.GetAllOrderedAsync();
            Snapshots = new ObservableCollection<InvestmentSnapshot>(snapshots);
            BuildAccountSummaries(Snapshots);
            SelectedAccount = AccountSummaries.FirstOrDefault();
            SelectedSnapshot = Snapshots.FirstOrDefault();
            StatusMessage = Snapshots.Any()
                ? $"Loaded {AccountSummaries.Count} account(s) from {Snapshots.Count} snapshot(s)."
                : "No investment snapshots were found in the database.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load snapshots: {ex.Message}";
            Snapshots.Clear();
            AccountSummaries.Clear();
            AccountHistory.Clear();
            CurrentAccountHoldings.Clear();
            AssetAllocations.Clear();
            TotalCurrentValue = 0m;
            SelectedSnapshot = null;
            SelectedAccount = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task QuickImportAsync()
    {
        await _navigationService.NavigateToAsync<InvestmentsViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedSnapshot))]
    public async Task DeleteSelectedSnapshotAsync()
    {
        if (SelectedSnapshot == null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Delete snapshot effective {SelectedSnapshot.EffectiveDateForDisplay:d} imported {SelectedSnapshot.ImportedAt:g} with {SelectedSnapshot.Holdings.Count} holding(s)?\n\nThis cannot be undone.",
            "Delete Investment Snapshot",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _investmentRepository.DeleteAsync(SelectedSnapshot.Id);
            StatusMessage = "Snapshot deleted.";
            await LoadSnapshotsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to delete snapshot: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task AddHoldingAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName))
        {
            StatusMessage = "Account name is required.";
            return;
        }

        if (NewMarketValue <= 0)
        {
            StatusMessage = "Market value must be greater than zero.";
            return;
        }

        var holding = new InvestmentHolding
        {
            AccountName = NewAccountName,
            Symbol = string.IsNullOrWhiteSpace(NewSymbol) ? null : NewSymbol.Trim(),
            Description = string.IsNullOrWhiteSpace(NewDescription) ? null : NewDescription.Trim(),
            Quantity = NewQuantity,
            Price = NewPrice,
            MarketValue = NewMarketValue,
            AssetClass = string.IsNullOrWhiteSpace(NewAssetClass) ? null : NewAssetClass.Trim(),
            PercentOfAccount = NewPercentOfAccount
        };

        var snapshot = new InvestmentSnapshot
        {
            EffectiveDate = DateTime.Today,
            ImportedAt = DateTime.UtcNow,
            SourceName = "Manual Entry",
            Holdings = new List<InvestmentHolding> { holding }
        };

        await _investmentRepository.InsertAsync(snapshot);
        StatusMessage = "Holding saved.";
        ClearManualEntry();
        await LoadSnapshotsAsync();
    }

    partial void OnSelectedSnapshotChanged(InvestmentSnapshot? value)
    {
        DeleteSelectedSnapshotCommand.NotifyCanExecuteChanged();
        SnapshotHoldings.Clear();

        if (value == null)
        {
            SelectedSnapshotTotal = 0m;
            return;
        }

        foreach (var holding in value.Holdings)
        {
            SnapshotHoldings.Add(new InvestmentHoldingRow(holding));
        }

        SelectedSnapshotTotal = value.TotalValue;
    }

    private bool CanDeleteSelectedSnapshot()
    {
        return SelectedSnapshot != null && !IsLoading;
    }

    partial void OnSelectedAccountChanged(InvestmentAccountSummaryRow? value)
    {
        AccountHistory.Clear();
        CurrentAccountHoldings.Clear();

        if (value == null)
        {
            return;
        }

        var accountSnapshots = GetAccountSnapshots(Snapshots, value.AccountName).ToList();

        for (var i = 0; i < accountSnapshots.Count; i++)
        {
            var accountSnapshot = accountSnapshots[i];
            var previousSnapshot = i + 1 < accountSnapshots.Count ? accountSnapshots[i + 1] : null;

            AccountHistory.Add(new InvestmentAccountHistoryRow(
                accountSnapshot.EffectiveDate,
                accountSnapshot.ImportedAt,
                accountSnapshot.SourceName,
                accountSnapshot.TotalValue,
                previousSnapshot?.TotalValue,
                accountSnapshot.Holdings.Count));
        }

        var currentSnapshot = accountSnapshots.FirstOrDefault();
        if (currentSnapshot == null)
        {
            return;
        }

        foreach (var holding in currentSnapshot.Holdings.OrderByDescending(h => h.MarketValue))
        {
            CurrentAccountHoldings.Add(new InvestmentHoldingRow(holding));
        }
    }

    private void BuildAccountSummaries(IEnumerable<InvestmentSnapshot> snapshots)
    {
        var accountSnapshots = GetAccountSnapshots(snapshots).ToList();
        var summaries = accountSnapshots
            .GroupBy(s => NormalizeAccountName(s.AccountName))
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(s => s.EffectiveDate)
                    .ThenByDescending(s => s.ImportedAt)
                    .ToList();
                var current = ordered[0];
                var previous = ordered.Skip(1).FirstOrDefault();

                return new InvestmentAccountSummaryRow(
                    current.AccountName,
                    current.TotalValue,
                    previous?.TotalValue,
                    current.EffectiveDate,
                    current.ImportedAt,
                    current.SourceName,
                    current.Holdings.Count);
            })
            .OrderByDescending(s => s.CurrentValue);

        AccountSummaries = new ObservableCollection<InvestmentAccountSummaryRow>(summaries);
        TotalCurrentValue = AccountSummaries.Sum(s => s.CurrentValue);
        BuildAssetAllocations(accountSnapshots);
    }

    private void BuildAssetAllocations(IEnumerable<AccountSnapshotProjection> accountSnapshots)
    {
        var currentAccountSnapshots = accountSnapshots
            .GroupBy(s => NormalizeAccountName(s.AccountName))
            .Select(group => group
                .OrderByDescending(s => s.EffectiveDate)
                .ThenByDescending(s => s.ImportedAt)
                .First())
            .ToList();

        var totalValue = currentAccountSnapshots.Sum(s => s.TotalValue);
        if (totalValue <= 0m)
        {
            AssetAllocations.Clear();
            return;
        }

        var allocations = currentAccountSnapshots
            .SelectMany(s => s.Holdings)
            .GroupBy(h => NormalizeAssetClass(h.AssetClass), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var value = group.Sum(h => h.MarketValue);

                return new InvestmentAssetAllocationRow(
                    group.Key,
                    value,
                    value / totalValue);
            })
            .Where(row => row.Value > 0m)
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.AssetClass)
            .ToList();

        AssetAllocations = new ObservableCollection<InvestmentAssetAllocationRow>(allocations);
    }

    private static IEnumerable<AccountSnapshotProjection> GetAccountSnapshots(
        IEnumerable<InvestmentSnapshot> snapshots,
        string? accountName = null)
    {
        var normalizedAccountName = accountName == null ? null : NormalizeAccountName(accountName);

        return snapshots
            .SelectMany(snapshot => snapshot.Holdings
                .GroupBy(holding => NormalizeAccountName(holding.AccountName))
                .Select(group =>
                {
                    var holdings = group.ToList();
                    var displayName = holdings
                        .Select(h => h.AccountName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

                    return new AccountSnapshotProjection(
                        string.IsNullOrWhiteSpace(displayName) ? "Unassigned" : displayName.Trim(),
                        snapshot.EffectiveDateForDisplay,
                        snapshot.ImportedAt,
                        snapshot.SourceName,
                        holdings.Sum(h => h.MarketValue),
                        holdings);
                }))
            .Where(snapshot => normalizedAccountName == null ||
                               NormalizeAccountName(snapshot.AccountName) == normalizedAccountName)
            .OrderByDescending(snapshot => snapshot.EffectiveDate)
            .ThenByDescending(snapshot => snapshot.ImportedAt);
    }

    private static string NormalizeAccountName(string? accountName)
    {
        return string.IsNullOrWhiteSpace(accountName)
            ? "unassigned"
            : accountName.Trim().ToUpperInvariant();
    }

    private static string NormalizeAssetClass(string? assetClass)
    {
        return string.IsNullOrWhiteSpace(assetClass)
            ? "Unclassified"
            : assetClass.Trim();
    }

    private void ClearManualEntry()
    {
        NewAccountName = string.Empty;
        NewSymbol = string.Empty;
        NewDescription = string.Empty;
        NewQuantity = 0;
        NewPrice = 0;
        NewMarketValue = 0;
        NewAssetClass = string.Empty;
        NewPercentOfAccount = null;
    }

    private sealed record AccountSnapshotProjection(
        string AccountName,
        DateTime EffectiveDate,
        DateTime ImportedAt,
        string? SourceName,
        decimal TotalValue,
        List<InvestmentHolding> Holdings);
}

public sealed class InvestmentAccountSummaryRow
{
    public InvestmentAccountSummaryRow(
        string accountName,
        decimal currentValue,
        decimal? previousValue,
        DateTime effectiveDate,
        DateTime lastUpdated,
        string? sourceName,
        int holdingsCount)
    {
        AccountName = accountName;
        CurrentValue = currentValue;
        PreviousValue = previousValue;
        EffectiveDate = effectiveDate;
        LastUpdated = lastUpdated;
        SourceName = sourceName;
        HoldingsCount = holdingsCount;
    }

    public string AccountName { get; }

    public decimal CurrentValue { get; }

    public decimal? PreviousValue { get; }

    public decimal Change => PreviousValue.HasValue ? CurrentValue - PreviousValue.Value : 0m;

    public decimal? ChangePercent => PreviousValue is > 0m
        ? Change / PreviousValue.Value
        : null;

    public DateTime EffectiveDate { get; }

    public DateTime LastUpdated { get; }

    public string? SourceName { get; }

    public int HoldingsCount { get; }
}

public sealed class InvestmentAssetAllocationRow
{
    public InvestmentAssetAllocationRow(string assetClass, decimal value, decimal percent)
    {
        AssetClass = assetClass;
        Value = value;
        Percent = percent;
    }

    public string AssetClass { get; }

    public decimal Value { get; }

    public decimal Percent { get; }

    public string DisplayText => $"{AssetClass}: {Percent:P1}";
}

public sealed class InvestmentAccountHistoryRow
{
    public InvestmentAccountHistoryRow(
        DateTime effectiveDate,
        DateTime importedAt,
        string? sourceName,
        decimal totalValue,
        decimal? previousValue,
        int holdingsCount)
    {
        EffectiveDate = effectiveDate;
        ImportedAt = importedAt;
        SourceName = sourceName;
        TotalValue = totalValue;
        PreviousValue = previousValue;
        HoldingsCount = holdingsCount;
    }

    public DateTime EffectiveDate { get; }

    public DateTime ImportedAt { get; }

    public string? SourceName { get; }

    public decimal TotalValue { get; }

    public decimal? PreviousValue { get; }

    public decimal Change => PreviousValue.HasValue ? TotalValue - PreviousValue.Value : 0m;

    public decimal? ChangePercent => PreviousValue is > 0m
        ? Change / PreviousValue.Value
        : null;

    public int HoldingsCount { get; }
}
