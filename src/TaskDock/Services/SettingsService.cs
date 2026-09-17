using System.Text.Json;
using Microsoft.Win32;
using TaskDock.Models;

namespace TaskDock.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        AppPaths.EnsureCreated();
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsPath), Options) ?? new AppSettings();
        }
        catch { Current = new AppSettings(); }
    }

    public void Save()
    {
        AppPaths.EnsureCreated();
        File.WriteAllText(AppPaths.SettingsPath, JsonSerializer.Serialize(Current, Options));
    }

    public void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enabled)
            key?.SetValue("TaskDock", $"\"{Environment.ProcessPath}\" --background");
        else
            key?.DeleteValue("TaskDock", false);
        Current.StartWithWindows = enabled;
        Save();
    }
}
