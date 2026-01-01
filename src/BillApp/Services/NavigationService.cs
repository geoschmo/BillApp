using System.Windows;
using BillApp.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.Services;

/// <summary>
/// Implementation of navigation service.
/// Manages ViewModel navigation similar to Angular Router.
///
/// How it works:
/// 1. MainWindow binds its ContentControl to CurrentView
/// 2. WPF uses DataTemplates (defined in App.xaml) to display the correct View for each ViewModel
/// 3. This is like Angular's router-outlet + route config
/// </summary>
public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<ViewModelBase> _navigationStack = new();

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool CanGoBack => _navigationStack.Count > 0;

    public async Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase
    {
        await NavigateToAsync<TViewModel>(null);
    }

    public async Task NavigateToAsync<TViewModel>(object? parameter) where TViewModel : ViewModelBase
    {
        // Notify current view it's being navigated away from
        if (CurrentView != null)
        {
            await CurrentView.OnNavigatedFromAsync();
            _navigationStack.Push(CurrentView);
        }

        // Get the new ViewModel from DI container
        var viewModel = (TViewModel)_serviceProvider.GetService(typeof(TViewModel))!;

        // Notify new view it's being navigated to
        await viewModel.OnNavigatedToAsync(parameter);

        // Update current view (triggers UI update via data binding)
        CurrentView = viewModel;
    }

    public async Task GoBackAsync()
    {
        if (!CanGoBack)
            return;

        if (CurrentView != null)
        {
            await CurrentView.OnNavigatedFromAsync();
        }

        var previousView = _navigationStack.Pop();
        await previousView.OnNavigatedToAsync();
        CurrentView = previousView;
    }
}
