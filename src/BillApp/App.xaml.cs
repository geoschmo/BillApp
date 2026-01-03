using System.Windows;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Infrastructure.Data;
using BillApp.Infrastructure.Repositories;
using BillApp.Infrastructure.Security;
using BillApp.Infrastructure.Services;
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

    /// <summary>
    /// Gets the application's service provider for resolving dependencies.
    /// </summary>
    public static IServiceProvider Services => ((App)Current)._serviceProvider;

    public App()
    {
        // Build DI container (like Angular's dependency injection)
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Security services (singleton - initialize once with keys)
        services.AddSingleton<KeyManager>();
        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<IEncryptionService, EncryptionService>();

        // Database context with encryption (singleton - one connection for the app)
        // Uses factory pattern to inject the encryption password
        services.AddSingleton<LiteDbContext>(sp =>
        {
            var keyManager = sp.GetRequiredService<KeyManager>();
            var password = keyManager.GetOrCreateDatabaseKey();
            return new LiteDbContext(password: password);
        });
        services.AddSingleton<DatabaseInitializer>();

        // Repositories (transient - new instance each time, shares DbContext)
        services.AddTransient<IBillRepository, BillRepository>();
        services.AddTransient<ICategoryRepository, CategoryRepository>();
        services.AddTransient<IAccountRepository, AccountRepository>();

        // Services (singleton = same instance everywhere)
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IAutoBackupService, AutoBackupService>();

        // ViewModels (transient = new instance each time)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<BillListViewModel>();
        services.AddTransient<BillEditViewModel>();
        services.AddTransient<AccountListViewModel>();
        services.AddTransient<AccountEditViewModel>();
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

        // Load user settings
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        settingsService.Load();

        // Migrate existing unencrypted database if needed
        var migrator = _serviceProvider.GetRequiredService<DatabaseMigrator>();
        migrator.MigrateIfNeeded();

        // Initialize database with default data
        var dbInitializer = _serviceProvider.GetRequiredService<DatabaseInitializer>();
        dbInitializer.SeedDefaultCategories();

        // Get MainWindow from DI container
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

        mainWindow.DataContext = viewModel;

        // Apply saved window settings
        var settings = settingsService.Settings;
        if (settings.IsMaximized)
        {
            mainWindow.WindowState = WindowState.Maximized;
        }
        else
        {
            mainWindow.Left = settings.WindowLeft;
            mainWindow.Top = settings.WindowTop;
            mainWindow.Width = settings.WindowWidth;
            mainWindow.Height = settings.WindowHeight;
        }

        // Initialize navigation (navigate to Dashboard)
        await viewModel.InitializeAsync();

        // Start auto-backup service
        var autoBackupService = _serviceProvider.GetRequiredService<IAutoBackupService>();
        autoBackupService.Start();

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop auto-backup service
        var autoBackupService = _serviceProvider.GetService<IAutoBackupService>();
        autoBackupService?.Dispose();

        // Dispose the database context when the app exits
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }
}
