using System.Collections.ObjectModel;
using System.Windows.Input;
using TaskDock.Data;
using TaskDock.Infrastructure;
using TaskDock.Models;
using TaskDock.Services;

namespace TaskDock.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly TaskRepository _repository;
    private readonly SettingsService _settings;
    private readonly List<TaskItem> _all = [];
    private string _selectedView = "Today";
    private string _quickInput = string.Empty;
    private string _searchText = string.Empty;
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private TaskItem? _lastDeleted;
    private string _toastMessage = string.Empty;
    private string _statusFilter = "All";
    private string _priorityFilter = "All";
    private string _sortMode = "Due";
    private string _historyRange = "Month";

    public MainViewModel(TaskRepository repository, SettingsService settings)
    {
        _repository = repository;
        _settings = settings;
        AddQuickCommand = new RelayCommand(_ => AddQuick(), _ => !string.IsNullOrWhiteSpace(QuickInput));
        ChangeStatusCommand = new RelayCommand(ChangeStatus);
        DeleteCommand = new RelayCommand(Delete);
        RestoreCommand = new RelayCommand(Restore);
        DeletePermanentlyCommand = new RelayCommand(DeletePermanently);
        ToggleTimerCommand = new RelayCommand(ToggleTimer);
        SnoozeCommand = new RelayCommand(Snooze);
        ArchiveCommand = new RelayCommand(Archive);
        UnarchiveCommand = new RelayCommand(Unarchive);
        OpenLinkCommand = new RelayCommand(OpenLink);
        OpenAttachmentCommand = new RelayCommand(OpenAttachment);
        EditCommand = new RelayCommand(item => { if (item is TaskItem task) EditRequested?.Invoke(this, task); });
        UndoDeleteCommand = new RelayCommand(_ => UndoDelete(), _ => _lastDeleted is not null);
        Reload();
    }

    public ObservableCollection<TaskItem> VisibleTasks { get; } = [];
    public ObservableCollection<TaskItem> PendingTasks { get; } = [];
    public ObservableCollection<TaskItem> InProgressTasks { get; } = [];
    public ObservableCollection<TaskItem> CompletedTasks { get; } = [];
    public ObservableCollection<TaskItem> TodayFocusTasks { get; } = [];
    public ObservableCollection<TaskItem> CompletedTodayTasks { get; } = [];
    public ObservableCollection<CalendarDayItem> CalendarDays { get; } = [];
    public AppSettings Settings => _settings.Current;

    public string SelectedView
    {
        get => _selectedView;
        set { if (SetProperty(ref _selectedView, value)) { OnPropertyChanged(nameof(PageTitle)); OnPropertyChanged(nameof(PageSubtitle)); ApplyFilter(); } }
    }
    public string PageTitle => SelectedView switch
    {
        "Today" => "今天",
        "All" => "全部任务",
        "Kanban" => "任务看板",
        "Calendar" => "日历",
        "History" => "工作记录",
        "Archive" => "归档",
        "Recycle" => "回收站",
        "Settings" => "设置",
        _ => "TaskDock"
    };
    public string PageSubtitle => SelectedView switch
    {
        "Today" => DateTime.Now.ToString("M月d日 dddd", new System.Globalization.CultureInfo("zh-CN")),
        "History" => "看见每一点进展",
        "Recycle" => "删除的任务保留 30 天",
        "Settings" => "按你的方式使用 TaskDock",
        _ => ""
    };
    public string QuickInput
    {
        get => _quickInput;
        set { if (SetProperty(ref _quickInput, value)) ((RelayCommand)AddQuickCommand).RaiseCanExecuteChanged(); }
    }
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            var date = value.Date;
            if (!SetProperty(ref _selectedDate, date)) return;
            if (date.Year != _calendarMonth.Year || date.Month != _calendarMonth.Month)
            {
                _calendarMonth = new DateTime(date.Year, date.Month, 1);
                OnPropertyChanged(nameof(CalendarMonthTitle));
            }
            ApplyFilter();
        }
    }
    public string ToastMessage { get => _toastMessage; set => SetProperty(ref _toastMessage, value); }
    public string StatusFilter { get => _statusFilter; set { if (SetProperty(ref _statusFilter, value)) ApplyFilter(); } }
    public string PriorityFilter { get => _priorityFilter; set { if (SetProperty(ref _priorityFilter, value)) ApplyFilter(); } }
    public string SortMode { get => _sortMode; set { if (SetProperty(ref _sortMode, value)) ApplyFilter(); } }
    public string HistoryRange { get => _historyRange; set { if (SetProperty(ref _historyRange, value)) ApplyFilter(); } }

    public int PendingCount => _all.Count(t => !t.IsDeleted && !t.IsArchived && t.Status != WorkStatus.Completed);
    public int OverdueCount => _all.Count(t => !t.IsDeleted && !t.IsArchived && t.Status != WorkStatus.Completed && t.DueAt?.Date < DateTime.Today);
    public int TodayDueCount => _all.Count(t => !t.IsDeleted && !t.IsArchived && t.Status != WorkStatus.Completed && t.DueAt?.Date == DateTime.Today);
    public int InProgressCount => _all.Count(t => !t.IsDeleted && !t.IsArchived && t.Status == WorkStatus.InProgress);
    public int CompletedTodayCount => _all.Count(t => !t.IsDeleted && t.CompletedAt?.Date == DateTime.Today);
    public int CompletedWeekCount => _all.Count(t => !t.IsDeleted && t.CompletedAt >= DateTime.Today.AddDays(-6));
    public int WeekMinutes => _all.Where(t => !t.IsDeleted && t.UpdatedAt >= DateTime.Today.AddDays(-6)).Sum(t => t.ActualMinutes);
    public int CompletionRate
    {
        get
        {
            var relevant = _all.Where(t => !t.IsDeleted && t.DueAt >= DateTime.Today.AddDays(-30) && t.DueAt <= DateTime.Now).ToList();
            return relevant.Count == 0 ? 0 : (int)Math.Round(relevant.Count(t => t.Status == WorkStatus.Completed) * 100d / relevant.Count);
        }
    }
    public string ProjectTimeSummary
    {
        get
        {
            var cutoff = HistoryRange switch { "Day" => DateTime.Today, "Week" => DateTime.Today.AddDays(-6), "Month" => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), _ => DateTime.MinValue };
            var groups = _all.Where(t => !t.IsDeleted && t.CompletedAt >= cutoff)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Project) ? "未分类" : t.Project)
                .Select(g => new { Name = g.Key, Minutes = g.Sum(t => t.ActualMinutes), Count = g.Count() })
                .OrderByDescending(g => g.Minutes).ThenByDescending(g => g.Count).Take(4).ToList();
            return groups.Count == 0 ? "暂无项目数据" : string.Join("  ·  ", groups.Select(g => g.Minutes > 0 ? $"{g.Name} {g.Minutes}分" : $"{g.Name} {g.Count}项"));
        }
    }
    public string CalendarMonthTitle => _calendarMonth.ToString("yyyy年 M月");
    public string SelectedDateTitle => SelectedDate.Date == DateTime.Today ? "今天的任务" : SelectedDate.ToString("M月d日 dddd", new System.Globalization.CultureInfo("zh-CN"));

    public ICommand AddQuickCommand { get; }
    public ICommand ChangeStatusCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand DeletePermanentlyCommand { get; }
    public ICommand ToggleTimerCommand { get; }
    public ICommand SnoozeCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand UnarchiveCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand OpenAttachmentCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand UndoDeleteCommand { get; }
    public event EventHandler<TaskItem>? EditRequested;

    public void Reload()
    {
        _all.Clear();
        _all.AddRange(_repository.GetAll());
        ApplyFilter();
    }

    public void SaveEdited(TaskItem edited)
    {
        var existing = _all.FirstOrDefault(t => t.Id == edited.Id);
        if (existing is null) _all.Add(edited);
        else
        {
            var index = _all.IndexOf(existing);
            _all[index] = edited;
        }
        _repository.Save(edited);
        ApplyFilter();
        ShowToast("任务已保存");
    }

    public void Reschedule(TaskItem task, DateTime date)
    {
        var time = task.DueAt?.TimeOfDay ?? TimeSpan.FromHours(18);
        task.DueAt = date.Date.Add(time);
        _repository.Save(task);
        SelectedDate = date.Date;
        ApplyFilter();
        ShowToast($"已移至 {date:M月d日}");
    }

    public void ChangeCalendarMonth(int offset)
    {
        _calendarMonth = _calendarMonth.AddMonths(offset);
        OnPropertyChanged(nameof(CalendarMonthTitle));
        BuildCalendarDays();
    }

    public void SelectCalendarDate(DateTime date) => SelectedDate = date;

    public void CompleteMany(IEnumerable<TaskItem> tasks)
    {
        foreach (var task in tasks.Where(t => !t.IsDeleted && t.Status != WorkStatus.Completed).ToList())
        {
            task.Status = WorkStatus.Completed;
            task.CompletedAt = DateTime.Now;
            if (task.IsTimerRunning) _repository.StopTimer(task, DateTime.Now);
            _repository.Save(task);
        }
        ApplyFilter();
        ShowToast("所选任务已完成");
    }

    public void DeleteMany(IEnumerable<TaskItem> tasks)
    {
        foreach (var task in tasks.Where(t => !t.IsDeleted).ToList()) _repository.SoftDelete(task);
        ApplyFilter();
        ShowToast("所选任务已移至回收站");
    }

    public void MoveToStatus(TaskItem task, WorkStatus status)
    {
        if (task.Status == status) return;
        SetStatus(task, status);
        ShowToast($"已移至{(status == WorkStatus.Pending ? "待处理" : status == WorkStatus.InProgress ? "进行中" : "已完成")}");
    }

    private void AddQuick()
    {
        var parsed = NaturalLanguageParser.Parse(QuickInput);
        var task = new TaskItem
        {
            Title = parsed.Title,
            DueAt = parsed.DueAt,
            ReminderAt = parsed.ReminderAt,
            Priority = parsed.Priority,
            RecurrenceRule = parsed.RecurrenceRule,
            Status = WorkStatus.Pending
        };
        _repository.Save(task);
        _all.Insert(0, task);
        QuickInput = string.Empty;
        ApplyFilter();
        ShowToast(parsed.DueAt.HasValue ? $"已添加 · {task.DueDisplay}" : "任务已添加");
    }

    private void ChangeStatus(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        var nextStatus = task.Status switch
        {
            WorkStatus.Pending => WorkStatus.InProgress,
            WorkStatus.InProgress => WorkStatus.Completed,
            _ => WorkStatus.Pending
        };
        SetStatus(task, nextStatus);
    }

    private void SetStatus(TaskItem task, WorkStatus status)
    {
        var wasCompleted = task.Status == WorkStatus.Completed;
        task.Status = status;
        task.CompletedAt = status == WorkStatus.Completed ? task.CompletedAt ?? DateTime.Now : null;
        if (status == WorkStatus.Completed && task.IsTimerRunning) _repository.StopTimer(task, DateTime.Now);
        _repository.Save(task);
        if (!wasCompleted && status == WorkStatus.Completed && !string.IsNullOrWhiteSpace(task.RecurrenceRule))
            CreateNextOccurrence(task);
        ApplyFilter();
    }

    private void CreateNextOccurrence(TaskItem completed)
    {
        var next = completed.Clone();
        next.Id = Guid.NewGuid().ToString("N");
        next.Status = WorkStatus.Pending;
        next.CompletedAt = null;
        next.ActualSeconds = 0;
        next.ActiveTimerStart = null;
        next.CreatedAt = next.UpdatedAt = DateTime.Now;
        if (next.DueAt.HasValue)
            next.DueAt = next.RecurrenceRule switch
            {
                "daily" => next.DueAt.Value.AddDays(1),
                "weekly" => next.DueAt.Value.AddDays(7),
                "monthly" => next.DueAt.Value.AddMonths(1),
                _ => next.DueAt
            };
        if (completed.ReminderAt.HasValue && completed.DueAt.HasValue && next.DueAt.HasValue)
            next.ReminderAt = next.DueAt.Value - (completed.DueAt.Value - completed.ReminderAt.Value);
        _repository.Save(next);
        _all.Insert(0, next);
    }

    private void Delete(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        if (task.IsTimerRunning) _repository.StopTimer(task, DateTime.Now);
        _repository.SoftDelete(task);
        _lastDeleted = task;
        ((RelayCommand)UndoDeleteCommand).RaiseCanExecuteChanged();
        ApplyFilter();
        ShowToast("任务已移至回收站 · 可撤销");
    }

    private void UndoDelete()
    {
        if (_lastDeleted is null) return;
        _repository.Restore(_lastDeleted);
        _lastDeleted = null;
        ((RelayCommand)UndoDeleteCommand).RaiseCanExecuteChanged();
        ApplyFilter();
        ShowToast("已撤销删除");
    }

    private void Restore(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        _repository.Restore(task);
        ApplyFilter();
    }

    private void DeletePermanently(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        _repository.DeletePermanently(task);
        _all.Remove(task);
        ApplyFilter();
    }

    private void ToggleTimer(object? parameter)
    {
        if (parameter is not TaskItem task || task.Status == WorkStatus.Completed) return;
        foreach (var running in _all.Where(t => t.IsTimerRunning && t.Id != task.Id).ToList()) _repository.StopTimer(running, DateTime.Now);
        if (task.IsTimerRunning) _repository.StopTimer(task, DateTime.Now);
        else { task.ActiveTimerStart = DateTime.Now; if (task.Status == WorkStatus.Pending) task.Status = WorkStatus.InProgress; _repository.Save(task); }
        ApplyFilter();
    }

    private void Snooze(object? parameter)
    {
        if (parameter is not TaskItem task || task.Status == WorkStatus.Completed) return;
        task.ReminderAt = DateTime.Now.AddMinutes(15);
        task.LastReminderAt = null;
        _repository.Save(task);
        ShowToast("15 分钟后再次提醒");
    }

    private void Archive(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        task.IsArchived = true;
        _repository.Save(task);
        ApplyFilter();
        ShowToast("任务已归档");
    }

    private void Unarchive(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        task.IsArchived = false;
        _repository.Save(task);
        ApplyFilter();
        ShowToast("任务已移出归档");
    }

    private void OpenLink(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        try
        {
            if (!ResourceLauncher.OpenWeb(task.Link)) ShowToast("网页链接无效");
        }
        catch (Exception ex) { ShowToast($"无法打开链接：{ex.Message}"); }
    }

    private void OpenAttachment(object? parameter)
    {
        if (parameter is not TaskItem task) return;
        try
        {
            if (!ResourceLauncher.OpenFile(task.AttachmentPath)) ShowToast("附件不存在或已被移动");
        }
        catch (Exception ex) { ShowToast($"无法打开附件：{ex.Message}"); }
    }

    private void ApplyFilter()
    {
        IEnumerable<TaskItem> query = _all;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var text = SearchText.Trim();
            query = query.Where(t => t.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || t.Notes.Contains(text, StringComparison.OrdinalIgnoreCase)
                || t.Project.Contains(text, StringComparison.OrdinalIgnoreCase)
                || t.Tags.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedView switch
        {
            "Today" => query.Where(t => !t.IsDeleted && !t.IsArchived &&
                ((t.Status != WorkStatus.Completed && (t.DueAt?.Date <= DateTime.Today || t.Status == WorkStatus.InProgress))
                 || t.CompletedAt?.Date == DateTime.Today)),
            "Calendar" => query.Where(t => !t.IsDeleted && !t.IsArchived && t.DueAt?.Date == SelectedDate.Date),
            "History" => query.Where(t => !t.IsDeleted && t.Status == WorkStatus.Completed && t.CompletedAt >= HistoryCutoff()),
            "Archive" => query.Where(t => !t.IsDeleted && t.IsArchived),
            "Recycle" => query.Where(t => t.IsDeleted),
            "Settings" => [],
            _ => query.Where(t => !t.IsDeleted && !t.IsArchived)
        };

        if (SelectedView == "All")
        {
            if (StatusFilter != "All" && Enum.TryParse<WorkStatus>(StatusFilter, out var status)) query = query.Where(t => t.Status == status);
            if (PriorityFilter != "All" && Enum.TryParse<TaskPriority>(PriorityFilter, out var priority)) query = query.Where(t => t.Priority == priority);
        }

        var sorted = SortMode switch
        {
            "Priority" => query.OrderByDescending(t => t.Priority).ThenBy(t => t.DueAt ?? DateTime.MaxValue).ToList(),
            "Created" => query.OrderByDescending(t => t.CreatedAt).ToList(),
            _ => query.OrderBy(t => t.Status).ThenBy(t => t.DueAt ?? DateTime.MaxValue).ThenByDescending(t => t.Priority).ToList()
        };
        Replace(VisibleTasks, sorted);
        var active = _all.Where(t => !t.IsDeleted && !t.IsArchived).ToList();
        Replace(PendingTasks, active.Where(t => t.Status == WorkStatus.Pending).OrderBy(t => t.DueAt ?? DateTime.MaxValue));
        Replace(InProgressTasks, active.Where(t => t.Status == WorkStatus.InProgress).OrderBy(t => t.DueAt ?? DateTime.MaxValue));
        Replace(CompletedTasks, active.Where(t => t.Status == WorkStatus.Completed).OrderByDescending(t => t.CompletedAt).Take(50));
        Replace(TodayFocusTasks, active.Where(t => t.Status != WorkStatus.Completed &&
            (t.DueAt?.Date <= DateTime.Today || t.Status == WorkStatus.InProgress))
            .OrderBy(t => t.DueAt?.Date < DateTime.Today ? 0 : t.DueAt?.Date == DateTime.Today ? 1 : 2)
            .ThenByDescending(t => t.Priority).ThenBy(t => t.DueAt ?? DateTime.MaxValue));
        Replace(CompletedTodayTasks, active.Where(t => t.Status == WorkStatus.Completed && t.CompletedAt?.Date == DateTime.Today)
            .OrderByDescending(t => t.CompletedAt));
        BuildCalendarDays();
        OnPropertyChanged(nameof(PendingCount)); OnPropertyChanged(nameof(OverdueCount)); OnPropertyChanged(nameof(TodayDueCount));
        OnPropertyChanged(nameof(InProgressCount)); OnPropertyChanged(nameof(CompletedTodayCount)); OnPropertyChanged(nameof(SelectedDateTitle));
        OnPropertyChanged(nameof(CompletedWeekCount)); OnPropertyChanged(nameof(WeekMinutes)); OnPropertyChanged(nameof(CompletionRate)); OnPropertyChanged(nameof(ProjectTimeSummary));
    }

    private void BuildCalendarDays()
    {
        var first = new DateTime(_calendarMonth.Year, _calendarMonth.Month, 1);
        var mondayOffset = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-mondayOffset);
        var counts = _all.Where(t => !t.IsDeleted && !t.IsArchived && t.DueAt.HasValue)
            .GroupBy(t => t.DueAt!.Value.Date)
            .ToDictionary(g => g.Key, g => g.Count());
        CalendarDays.Clear();
        for (var index = 0; index < 42; index++)
        {
            var date = start.AddDays(index);
            CalendarDays.Add(new CalendarDayItem
            {
                Date = date,
                IsCurrentMonth = date.Month == first.Month && date.Year == first.Year,
                IsSelected = date.Date == SelectedDate.Date,
                TaskCount = counts.GetValueOrDefault(date.Date)
            });
        }
    }

    private DateTime HistoryCutoff() => HistoryRange switch
    {
        "Day" => DateTime.Today,
        "Week" => DateTime.Today.AddDays(-6),
        "Month" => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
        _ => DateTime.MinValue
    };

    private static void Replace(ObservableCollection<TaskItem> target, IEnumerable<TaskItem> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private async void ShowToast(string message)
    {
        ToastMessage = message;
        await Task.Delay(2600);
        if (ToastMessage == message) ToastMessage = string.Empty;
    }
}
