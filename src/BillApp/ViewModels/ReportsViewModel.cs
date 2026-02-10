using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _title = "Reports";

    public ReportsViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        return base.OnNavigatedToAsync(parameter);
    }

    [RelayCommand]
    private async Task OpenAccountSummaryReportAsync()
    {
        await _navigationService.NavigateToAsync<AccountSummaryReportViewModel>();
    }

    [RelayCommand]
    private async Task OpenPaidBillsByCategoryReportAsync()
    {
        await _navigationService.NavigateToAsync<PaidBillsByCategoryReportViewModel>();
    }
}
