using System.ComponentModel;
using BillApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillApp.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Handles navigation between different sections of the app.
/// Similar to an Angular AppComponent with router-outlet.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        // Subscribe to NavigationService property changes
        // When CurrentView changes in NavigationService, we notify our binding
        if (_navigationService is INotifyPropertyChanged notifyPropertyChanged)
        {
            notifyPropertyChanged.PropertyChanged += OnNavigationServicePropertyChanged;
        }
    }

    private void OnNavigationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INavigationService.CurrentView))
        {
            OnPropertyChanged(nameof(CurrentView));
        }
    }

    /// <summary>
    /// The currently active ViewModel, bound to the main content area.
    /// This property delegates to NavigationService.CurrentView
    /// </summary>
    public ViewModelBase? CurrentView => _navigationService.CurrentView;

    /// <summary>
    /// Initialize the application by navigating to the dashboard.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _navigationService.NavigateToAsync<DashboardViewModel>();
    }

    [RelayCommand]
    private async Task NavigateToDashboard()
    {
        await _navigationService.NavigateToAsync<DashboardViewModel>();
    }

    [RelayCommand]
    private async Task NavigateToBills()
    {
        await _navigationService.NavigateToAsync<BillListViewModel>();
    }

    [RelayCommand]
    private async Task NavigateToAccounts()
    {
        await _navigationService.NavigateToAsync<AccountListViewModel>();
    }

    [RelayCommand]
    private async Task NavigateToBudget()
    {
        await _navigationService.NavigateToAsync<BudgetViewModel>();
    }

    [RelayCommand]
    private async Task NavigateToSecureNotes()
    {
        await _navigationService.NavigateToAsync<SecureNotesViewModel>();
    }

    [RelayCommand]
    private async Task NavigateToReports()
    {
        await _navigationService.NavigateToAsync<ReportsViewModel>();
    }

    [RelayCommand]
    private async Task NavigateToSettings()
    {
        await _navigationService.NavigateToAsync<SettingsViewModel>();
    }
}
