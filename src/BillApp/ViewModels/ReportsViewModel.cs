using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Reports";

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        // TODO: Load report data
        return base.OnNavigatedToAsync(parameter);
    }
}
