namespace BillApp.Infrastructure;

public static class AppStoragePaths
{
#if DEBUG
    public const string AppDataFolderName = "BillApp.Dev";
    public const bool IsDevelopmentBuild = true;
#else
    public const string AppDataFolderName = "BillApp";
    public const bool IsDevelopmentBuild = false;
#endif

    public static string LocalAppDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppDataFolderName);

    public static string DatabasePath => Path.Combine(LocalAppDataFolder, "billapp.db");

    public static string KeyFilePath => Path.Combine(LocalAppDataFolder, "key.dat");

    public static string SettingsPath => Path.Combine(LocalAppDataFolder, "settings.json");
}
