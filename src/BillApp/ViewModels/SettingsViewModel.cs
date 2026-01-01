using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Settings";

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        // TODO: Load settings
        return base.OnNavigatedToAsync(parameter);
    }
}
