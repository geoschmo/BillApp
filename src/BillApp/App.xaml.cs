using System.Windows;
using BillApp.Services;
using BillApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BillApp;

/// <summary>
/// Application entry point with dependency injection setup.
/// Similar to Angular's main.ts and AppModule for providers.
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        // Build DI container (like Angular's dependency injection)
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register services (singleton = same instance everywhere)
        services.AddSingleton<INavigationService, NavigationService>();

        // Register ViewModels (transient = new instance each time)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<BillListViewModel>();
        services.AddTransient<AccountListViewModel>();
        services.AddTransient<BudgetViewModel>();
        services.AddTransient<SecureNotesViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Register MainWindow
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Get MainWindow from DI container
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

        mainWindow.DataContext = viewModel;

        // Initialize navigation (navigate to Dashboard)
        await viewModel.InitializeAsync();

        mainWindow.Show();
    }
}
