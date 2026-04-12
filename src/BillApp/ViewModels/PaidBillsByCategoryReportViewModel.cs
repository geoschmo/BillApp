using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class PaidBillsByCategoryReportViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly IPayeeRepository _payeeRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _title = "Paid Bills by Category";

    [ObservableProperty]
    private DateTime _startDate = new DateTime(DateTime.Today.Year, 1, 1);

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<CategoryFilterItem> _categoryOptions = new();

    [ObservableProperty]
    private CategoryFilterItem? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<CategoryGroup> _categoryGroups = new();

    [ObservableProperty]
    private decimal _grandTotal;

    [ObservableProperty]
    private int _totalBillCount;

    [ObservableProperty]
    private bool _showReport;

    public PaidBillsByCategoryReportViewModel(
        IBillRepository billRepository,
        IPayeeRepository payeeRepository,
        ICategoryRepository categoryRepository,
        INavigationService navigationService)
    {
        _billRepository = billRepository;
        _payeeRepository = payeeRepository;
        _categoryRepository = categoryRepository;
        _navigationService = navigationService;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        IsLoading = true;
        ShowReport = false;

        try
        {
            // Load categories for the dropdown
            var categories = (await _categoryRepository.GetAllAsync())
                .OrderBy(c => c.Name)
                .ToList();

            var options = new List<CategoryFilterItem>
            {
                new CategoryFilterItem { Id = null, Name = "All Categories" }
            };

            options.AddRange(categories.Select(c => new CategoryFilterItem
            {
                Id = c.Id,
                Name = c.Name,
                Color = c.Color
            }));

            CategoryOptions = new ObservableCollection<CategoryFilterItem>(options);
            SelectedCategory = options.First(); // Default to "All"
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        IsLoading = true;

        try
        {
            // Get all bills in the date range that are paid
            var allBills = await _billRepository.GetByPaidDateRangeAsync(StartDate, EndDate);
            var paidBills = allBills.Where(b => b.Status == PaymentStatus.Paid).ToList();

            // Get all payees and categories
            var payees = (await _payeeRepository.GetAllWithCategoriesAsync()).ToDictionary(p => p.Id);
            var categories = (await _categoryRepository.GetAllAsync()).ToDictionary(c => c.Id);

            // Filter by selected category if not "All"
            if (SelectedCategory?.Id != null)
            {
                var selectedCategoryId = SelectedCategory.Id.Value;
                paidBills = paidBills
                    .Where(b =>
                    {
                        if (payees.TryGetValue(b.PayeeId, out var payee))
                        {
                            return payee.CategoryId == selectedCategoryId;
                        }
                        return false;
                    })
                    .ToList();
            }

            // Group bills by category
            var groupedBills = paidBills
                .GroupBy(b =>
                {
                    if (payees.TryGetValue(b.PayeeId, out var payee) && payee.CategoryId.HasValue)
                    {
                        return payee.CategoryId.Value;
                    }
                    return Guid.Empty; // Uncategorized
                })
                .OrderBy(g =>
                {
                    if (g.Key == Guid.Empty) return "ZZZZ"; // Put uncategorized at end
                    return categories.TryGetValue(g.Key, out var cat) ? cat.Name : "Unknown";
                })
                .Select(g =>
                {
                    var categoryName = "Uncategorized";
                    var categoryColor = "#607D8B";

                    if (g.Key != Guid.Empty && categories.TryGetValue(g.Key, out var category))
                    {
                        categoryName = category.Name;
                        categoryColor = category.Color;
                    }

                    var billItems = g
                        .OrderBy(b => b.PaidDate ?? b.DueDate)
                        .Select(b =>
                        {
                            var payee = payees.TryGetValue(b.PayeeId, out var p) ? p : null;
                            return new PaidBillReportItem
                            {
                                PayeeName = payee?.Name ?? "Unknown",
                                PaidDate = b.PaidDate ?? b.DueDate,
                                AmountPaid = b.AmountPaid,
                                PayeeNotes = payee?.Notes,
                                BillNotes = b.Notes,
                                Confirmation = b.Confirmation
                            };
                        })
                        .ToList();

                    return new CategoryGroup
                    {
                        CategoryName = categoryName,
                        CategoryColor = categoryColor,
                        Bills = new ObservableCollection<PaidBillReportItem>(billItems),
                        CategoryTotal = billItems.Sum(b => b.AmountPaid),
                        BillCount = billItems.Count
                    };
                })
                .ToList();

            CategoryGroups = new ObservableCollection<CategoryGroup>(groupedBills);
            GrandTotal = groupedBills.Sum(g => g.CategoryTotal);
            TotalBillCount = groupedBills.Sum(g => g.BillCount);
            ShowReport = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Print(Visual? visual)
    {
        if (visual == null)
            return;

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
        {
            printDialog.PrintVisual(visual, $"Paid Bills by Category - {StartDate:MM/dd/yyyy} to {EndDate:MM/dd/yyyy}");
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await _navigationService.GoBackAsync();
    }
}

/// <summary>
/// Represents a category option in the filter dropdown.
/// </summary>
public class CategoryFilterItem
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
}

/// <summary>
/// Represents a category group with its bills.
/// </summary>
public class CategoryGroup
{
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = "#607D8B";
    public ObservableCollection<PaidBillReportItem> Bills { get; set; } = new();
    public decimal CategoryTotal { get; set; }
    public int BillCount { get; set; }

    public string CategoryTotalDisplay => CategoryTotal.ToString("C");
}

/// <summary>
/// Represents a paid bill item for display in the report.
/// </summary>
public class PaidBillReportItem
{
    public string PayeeName { get; set; } = string.Empty;
    public DateTime PaidDate { get; set; }
    public decimal AmountPaid { get; set; }
    public string? PayeeNotes { get; set; }
    public string? BillNotes { get; set; }
    public string? Confirmation { get; set; }

    public string PaidDateDisplay => PaidDate.ToString("MM/dd/yy");
    public string AmountPaidDisplay => AmountPaid.ToString("N2");

    /// <summary>
    /// Combined notes from confirmation, payee notes, and bill notes.
    /// </summary>
    public string CombinedNotes
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Confirmation))
                parts.Add($"Conf: {Confirmation}");

            if (!string.IsNullOrWhiteSpace(PayeeNotes))
                parts.Add($"Payee: {PayeeNotes}");

            if (!string.IsNullOrWhiteSpace(BillNotes))
                parts.Add($"Bill: {BillNotes}");

            return string.Join(" | ", parts);
        }
    }
}
