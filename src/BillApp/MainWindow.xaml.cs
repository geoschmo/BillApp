using System.Windows;

namespace BillApp;

/// <summary>
/// Main application window.
/// The DataContext (ViewModel) is set in App.xaml.cs via DI.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
