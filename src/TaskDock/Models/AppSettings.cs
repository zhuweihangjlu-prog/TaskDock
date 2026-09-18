namespace TaskDock.Models;

public sealed class AppSettings
{
    public bool FirstRun { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool AutoHide { get; set; } = true;
    public bool IsPinned { get; set; }
    public bool DailyReviewEnabled { get; set; } = true;
    public string DailyReviewTime { get; set; } = "09:00";
    public string Theme { get; set; } = "System";
    public string Accent { get; set; } = "Blue";
    public string Hotkey { get; set; } = "Ctrl+Alt+T";
    public string DisplayDeviceName { get; set; } = string.Empty;
    public string DockEdge { get; set; } = "Right";
    public double PanelWidth { get; set; } = 440;
    public bool IsFloating { get; set; }
    public DateTime? LastAutomaticBackup { get; set; }
}
