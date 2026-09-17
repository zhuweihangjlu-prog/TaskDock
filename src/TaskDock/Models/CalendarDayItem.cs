namespace TaskDock.Models;

public sealed class CalendarDayItem
{
    public required DateTime Date { get; init; }
    public required bool IsCurrentMonth { get; init; }
    public required bool IsSelected { get; init; }
    public bool IsToday => Date.Date == DateTime.Today;
    public string DayNumber => Date.Day.ToString();
    public int TaskCount { get; init; }
    public string TaskCountText => TaskCount == 0 ? string.Empty : TaskCount > 9 ? "9+" : TaskCount.ToString();
}
