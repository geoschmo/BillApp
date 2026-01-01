using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _welcomeMessage = "Welcome to BillApp";

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        // TODO: Load dashboard data (upcoming bills, account summaries, etc.)
        return base.OnNavigatedToAsync(parameter);
    }
}
