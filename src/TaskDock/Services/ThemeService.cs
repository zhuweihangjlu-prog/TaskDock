using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace TaskDock.Services;

public static class ThemeService
{
    public static void Apply(string requestedTheme, string accent = "Blue")
    {
        var dark = requestedTheme == "Dark" || requestedTheme == "System" && SystemUsesDarkTheme();
        Set("AppBackgroundBrush", dark ? "#111827" : "#F4F6FA");
        Set("SurfaceBrush", dark ? "#1F2937" : "#FFFFFFFF");
        Set("SurfaceMutedBrush", dark ? "#182230" : "#FFF7F8FC");
        Set("TextPrimaryBrush", dark ? "#F3F4F6" : "#FF1F2937");
        Set("TextSecondaryBrush", dark ? "#9CA3AF" : "#FF667085");
        Set("BorderBrush", dark ? "#374151" : "#FFE4E7EC");
        var accentColor = accent switch { "Purple" => "#FF7C5CFC", "Teal" => "#FF0F9D8A", "Rose" => "#FFE05278", _ => "#FF4F6BED" };
        var accentSoft = accent switch
        {
            "Purple" => dark ? "#392F63" : "#FFF0ECFF",
            "Teal" => dark ? "#183F3C" : "#FFE3F7F3",
            "Rose" => dark ? "#512A3B" : "#FFFFE9F0",
            _ => dark ? "#2A365F" : "#FFE8EDFF"
        };
        Set("AccentBrush", accentColor);
        Set("AccentSoftBrush", accentSoft);
    }

    private static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch { return false; }
    }

    private static void Set(string key, string color) => Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
