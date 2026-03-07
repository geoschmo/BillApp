using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Views.Investments;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class InvestmentsOverviewViewModel : ViewModelBase
{
    private readonly IInvestmentRepository _investmentRepository;

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

    public InvestmentsOverviewViewModel(IInvestmentRepository investmentRepository)
    {
        _investmentRepository = investmentRepository;
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
            SelectedSnapshot = Snapshots.FirstOrDefault();
            StatusMessage = Snapshots.Any()
                ? $"Loaded {Snapshots.Count} snapshot(s)."
                : "No snapshots yet. Use Quick Import or Add Holding.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load snapshots: {ex.Message}";
            Snapshots.Clear();
            SelectedSnapshot = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task QuickImportAsync()
    {
        var dialog = new InvestmentsImportDialog
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();
        await LoadSnapshotsAsync();
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
}
