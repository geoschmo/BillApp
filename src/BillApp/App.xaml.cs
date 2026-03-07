using System.Windows;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Interfaces.Services;
using BillApp.Infrastructure.Data;
using BillApp.Infrastructure.Repositories;
using BillApp.Infrastructure.Security;
using BillApp.Infrastructure.Services;
using BillApp.Services;
using BillApp.ViewModels;
using BillApp.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace BillApp;

/// <summary>
/// Application entry point with dependency injection setup.
/// Similar to Angular's main.ts and AppModule for providers.
/// </summary>
public partial class App : System.Windows.Application
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
        services.AddSingleton<DatabaseMigration>();

        // Repositories (transient - new instance each time, shares DbContext)
        services.AddTransient<IBillRepository, BillRepository>();
        services.AddTransient<ICategoryRepository, CategoryRepository>();
        services.AddTransient<IPayeeRepository, PayeeRepository>();
        services.AddTransient<IInvestmentRepository, InvestmentRepository>();

        // Services (singleton = same instance everywhere)
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IAutoBackupService, AutoBackupService>();
        services.AddSingleton<IImportService, CsvImportService>();
        services.AddSingleton<IInvestmentImportService, InvestmentImportService>();

        services.AddTransient<InvestmentsOverviewViewModel>();

        // ViewModels (transient = new instance each time)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<BillListViewModel>();
        services.AddTransient<BillEditViewModel>();
        services.AddTransient<PayeeListViewModel>();
        services.AddTransient<PayeeEditViewModel>();
        services.AddTransient<BudgetViewModel>();
        services.AddTransient<SecureNotesViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<AccountSummaryReportViewModel>();
        services.AddTransient<PaidBillsByCategoryReportViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<InvestmentsViewModel>();
        services.AddTransient<InvestmentsOverviewViewModel>();

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

        // Run database schema migration (Account -> Payee)
        var schemaMigration = _serviceProvider.GetRequiredService<DatabaseMigration>();
        schemaMigration.MigrateIfNeeded();

        // Initialize database - check if this is first run
        var dbInitializer = _serviceProvider.GetRequiredService<DatabaseInitializer>();

        if (dbInitializer.IsFreshDatabase())
        {
            // Show first-run dialog to let user choose empty or sample data
            var setupDialog = new DatabaseSetupDialog();
            if (setupDialog.ShowDialog() == true)
            {
                if (setupDialog.UseSampleData)
                {
                    dbInitializer.SeedSampleData();
                }
                else
                {
                    dbInitializer.SeedDefaultCategories();
                }
            }
            else
            {
                // User closed dialog without choosing - just seed default categories
                dbInitializer.SeedDefaultCategories();
            }
        }
        else
        {
            // Existing database - just ensure categories exist
            dbInitializer.SeedDefaultCategories();
        }

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

        // Set main window and switch shutdown mode so app closes when main window closes
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

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
