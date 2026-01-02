using System.Windows;
using BillApp.Services;

namespace BillApp;

/// <summary>
/// Main application window.
/// The DataContext (ViewModel) is set in App.xaml.cs via DI.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;

    public MainWindow(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save window position and size
        var settings = _settingsService.Settings;
        settings.IsMaximized = WindowState == WindowState.Maximized;

        // Only save position/size if not maximized (RestoreBounds gives the normal window size)
        if (WindowState == WindowState.Maximized)
        {
            settings.WindowLeft = RestoreBounds.Left;
            settings.WindowTop = RestoreBounds.Top;
            settings.WindowWidth = RestoreBounds.Width;
            settings.WindowHeight = RestoreBounds.Height;
        }
        else
        {
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
        }

        _settingsService.Save();
    }
}
