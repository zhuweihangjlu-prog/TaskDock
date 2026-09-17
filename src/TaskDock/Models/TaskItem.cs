using TaskDock.Infrastructure;

namespace TaskDock.Models;

public enum WorkStatus { Pending = 0, InProgress = 1, Completed = 2 }
public enum TaskPriority { Low = 0, Normal = 1, High = 2, Urgent = 3 }

public sealed class SubtaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public sealed class TaskItem : ObservableObject
{
    private string _title = string.Empty;
    private string _notes = string.Empty;
    private WorkStatus _status;
    private TaskPriority _priority = TaskPriority.Normal;
    private DateTime? _dueAt;
    private DateTime? _reminderAt;
    private string _project = string.Empty;
    private string _tags = string.Empty;
    private string _recurrenceRule = string.Empty;
    private string _link = string.Empty;
    private string _attachmentPath = string.Empty;
    private int _actualSeconds;
    private DateTime? _activeTimerStart;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public WorkStatus Status { get => _status; set { if (SetProperty(ref _status, value)) { OnPropertyChanged(nameof(StatusGlyph)); OnPropertyChanged(nameof(IsCompleted)); } } }
    public TaskPriority Priority { get => _priority; set => SetProperty(ref _priority, value); }
    public DateTime? DueAt { get => _dueAt; set { if (SetProperty(ref _dueAt, value)) { OnPropertyChanged(nameof(DueDisplay)); OnPropertyChanged(nameof(IsOverdue)); } } }
    public DateTime? ReminderAt { get => _reminderAt; set => SetProperty(ref _reminderAt, value); }
    public string Project { get => _project; set => SetProperty(ref _project, value); }
    public string Tags { get => _tags; set => SetProperty(ref _tags, value); }
    public string RecurrenceRule { get => _recurrenceRule; set => SetProperty(ref _recurrenceRule, value); }
    public string Link { get => _link; set => SetProperty(ref _link, value); }
    public string AttachmentPath { get => _attachmentPath; set => SetProperty(ref _attachmentPath, value); }
    public List<SubtaskItem> Subtasks { get; set; } = [];
    public int EstimatedMinutes { get; set; }
    public int ActualSeconds
    {
        get => _actualSeconds;
        set
        {
            if (!SetProperty(ref _actualSeconds, Math.Max(0, value))) return;
            OnPropertyChanged(nameof(ActualMinutes));
            OnPropertyChanged(nameof(TimeDisplay));
        }
    }
    public int ActualMinutes
    {
        get => ActualSeconds / 60;
        set => ActualSeconds = Math.Max(0, value) * 60;
    }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? LastReminderAt { get; set; }
    public DateTime? ActiveTimerStart { get => _activeTimerStart; set { if (SetProperty(ref _activeTimerStart, value)) { OnPropertyChanged(nameof(IsTimerRunning)); OnPropertyChanged(nameof(TimerGlyph)); } } }

    public bool IsCompleted => Status == WorkStatus.Completed;
    public bool IsTimerRunning => ActiveTimerStart.HasValue;
    public bool IsOverdue => DueAt.HasValue && DueAt.Value < DateTime.Now && Status != WorkStatus.Completed;
    public string StatusGlyph => Status switch { WorkStatus.Pending => "○", WorkStatus.InProgress => "◐", _ => "✓" };
    public string TimerGlyph => IsTimerRunning ? "\uE769" : "\uE768";
    public string DueDisplay => DueAt switch
    {
        null => string.Empty,
        var d when d.Value.Date == DateTime.Today => $"今天 {d.Value:HH:mm}",
        var d when d.Value.Date == DateTime.Today.AddDays(1) => $"明天 {d.Value:HH:mm}",
        var d => d.Value.ToString("M月d日 HH:mm")
    };
    public string TimeDisplay => ActualSeconds switch
    {
        <= 0 => string.Empty,
        < 60 => $"{ActualSeconds} 秒",
        < 3600 => $"{ActualSeconds / 60} 分钟",
        _ => $"{ActualSeconds / 3600}小时 {(ActualSeconds % 3600) / 60}分"
    };

    public TaskItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        Notes = Notes,
        Status = Status,
        Priority = Priority,
        DueAt = DueAt,
        ReminderAt = ReminderAt,
        Project = Project,
        Tags = Tags,
        RecurrenceRule = RecurrenceRule,
        Link = Link,
        AttachmentPath = AttachmentPath,
        Subtasks = Subtasks.Select(s => new SubtaskItem { Id = s.Id, Title = s.Title, IsCompleted = s.IsCompleted }).ToList(),
        EstimatedMinutes = EstimatedMinutes,
        ActualSeconds = ActualSeconds,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        CompletedAt = CompletedAt,
        IsDeleted = IsDeleted,
        DeletedAt = DeletedAt,
        IsArchived = IsArchived,
        LastReminderAt = LastReminderAt,
        ActiveTimerStart = ActiveTimerStart
    };
}
