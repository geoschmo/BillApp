using System.Collections.ObjectModel;
using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IBillRepository _billRepository;
    private readonly IPayeeRepository _payeeRepository;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to BillApp";

    [ObservableProperty]
    private ObservableCollection<Bill> _recentlyPaidBills = new();

    [ObservableProperty]
    private ObservableCollection<Bill> _upcomingBills = new();

    public DashboardViewModel(
        IBillRepository billRepository,
        IPayeeRepository payeeRepository)
    {
        _billRepository = billRepository;
        _payeeRepository = payeeRepository;
    }

    public override async Task OnNavigatedToAsync(object? parameter = null)
    {
        await LoadDashboardDataAsync();
        await base.OnNavigatedToAsync(parameter);
    }

    private async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            var allBills = (await _billRepository.GetAllAsync()).ToList();
            var payees = (await _payeeRepository.GetAllAsync()).ToDictionary(p => p.Id);

            // Populate payee navigation property for display
            foreach (var bill in allBills)
            {
                if (payees.TryGetValue(bill.PayeeId, out var payee))
                {
                    bill.Payee = payee;
                }
            }

            // Last 5 paid bills (most recently paid first)
            var recentlyPaid = allBills
                .Where(b => b.Status == PaymentStatus.Paid && b.PaidDate.HasValue)
                .OrderByDescending(b => b.PaidDate)
                .Take(5)
                .ToList();
            RecentlyPaidBills = new ObservableCollection<Bill>(recentlyPaid);

            // Next 5 upcoming bills (soonest due first)
            var upcoming = allBills
                .Where(b => b.Status == PaymentStatus.Pending || b.Status == PaymentStatus.Overdue)
                .OrderBy(b => b.DueDate)
                .Take(5)
                .ToList();
            UpcomingBills = new ObservableCollection<Bill>(upcoming);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
