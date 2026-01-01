using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class AccountListViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Accounts";

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        // TODO: Load accounts from repository
        return base.OnNavigatedToAsync(parameter);
    }
}
