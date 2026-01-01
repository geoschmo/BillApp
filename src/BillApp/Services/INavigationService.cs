using BillApp.ViewModels;

namespace BillApp.Services;

/// <summary>
/// Service for navigating between ViewModels/Views.
/// Similar to Angular's Router service.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// The currently displayed ViewModel.
    /// </summary>
    ViewModelBase? CurrentView { get; }

    /// <summary>
    /// Navigate to a ViewModel by type.
    /// </summary>
    Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>
    /// Navigate to a ViewModel with a parameter.
    /// </summary>
    Task NavigateToAsync<TViewModel>(object? parameter) where TViewModel : ViewModelBase;

    /// <summary>
    /// Whether navigation history exists.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Navigate to the previous ViewModel.
    /// </summary>
    Task GoBackAsync();
}
