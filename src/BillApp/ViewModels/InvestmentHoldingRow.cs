using BillApp.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class InvestmentHoldingRow : ObservableObject
{
    public InvestmentHoldingRow()
    {
    }

    public InvestmentHoldingRow(InvestmentHolding holding)
    {
        AccountName = holding.AccountName;
        Symbol = holding.Symbol;
        Description = holding.Description;
        Quantity = holding.Quantity;
        Price = holding.Price;
        MarketValue = holding.MarketValue;
        AssetClass = holding.AssetClass;
        PercentOfAccount = holding.PercentOfAccount;
        SourceLine = holding.SourceLine;
    }

    [ObservableProperty]
    private string _accountName = string.Empty;

    [ObservableProperty]
    private string? _symbol;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private decimal _quantity;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    private decimal _marketValue;

    [ObservableProperty]
    private string? _assetClass;

    [ObservableProperty]
    private decimal? _percentOfAccount;

    [ObservableProperty]
    private string? _sourceLine;

    public InvestmentHolding ToDomain()
    {
        return new InvestmentHolding
        {
            AccountName = AccountName,
            Symbol = Symbol,
            Description = Description,
            Quantity = Quantity,
            Price = Price,
            MarketValue = MarketValue,
            AssetClass = AssetClass,
            PercentOfAccount = PercentOfAccount,
            SourceLine = SourceLine
        };
    }
}
