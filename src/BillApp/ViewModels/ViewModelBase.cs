using CommunityToolkit.Mvvm.ComponentModel;

namespace BillApp.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// Similar to a base Angular component class.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Called when navigating to this ViewModel.
    /// Override to load data or initialize state.
    /// </summary>
    public virtual Task OnNavigatedToAsync(object? parameter = null)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when navigating away from this ViewModel.
    /// Override to cleanup or save state.
    /// </summary>
    public virtual Task OnNavigatedFromAsync()
    {
        return Task.CompletedTask;
    }
}
