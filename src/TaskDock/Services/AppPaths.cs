namespace TaskDock.Services;

public static class AppPaths
{
    public static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"));
    public static string DataDirectory
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("TASKDOCK_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
            return IsPortable ? Path.Combine(AppContext.BaseDirectory, "Data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskDock");
        }
    }
    public static string DatabasePath => Path.Combine(DataDirectory, "taskdock.db");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string BackupDirectory => Path.Combine(DataDirectory, "Backups");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }
}
