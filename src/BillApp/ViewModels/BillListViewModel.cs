using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class BillListViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Bills";

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        // TODO: Load bills from repository
        return base.OnNavigatedToAsync(parameter);
    }
}
