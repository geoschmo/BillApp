using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class BudgetViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Budget";

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        // TODO: Load budget data
        return base.OnNavigatedToAsync(parameter);
    }
}
