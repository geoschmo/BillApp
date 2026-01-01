using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

public partial class SecureNotesViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Secure Notes";

    public override Task OnNavigatedToAsync(object? parameter = null)
    {
        // TODO: Load secure notes from repository
        return base.OnNavigatedToAsync(parameter);
    }
}
