using TaskDock.Data;
using TaskDock.Models;
using TaskDock.Services;
using TaskDock.ViewModels;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TaskDock.Tests;

public sealed class RepositoryTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"TaskDockTests-{Guid.NewGuid():N}");

    public RepositoryTests()
    {
        Environment.SetEnvironmentVariable("TASKDOCK_DATA_DIR", _tempDirectory);
    }

    [Fact]
    public void PersistsDeletesRestoresTimesAndBacksUpTasks()
    {
        var repository = new TaskRepository();
        repository.Initialize();
        var task = new TaskItem { Title = "验证本地存储", Project = "TaskDock", DueAt = DateTime.Today.AddHours(18) };

        repository.Save(task);
        var saved = Assert.Single(repository.GetAll());
        Assert.Equal("验证本地存储", saved.Title);

        saved.ActiveTimerStart = DateTime.Now.AddMinutes(-10);
        repository.Save(saved);
        repository.StopTimer(saved, DateTime.Now);
        Assert.True(saved.ActualMinutes >= 10);

        repository.SoftDelete(saved);
        Assert.True(Assert.Single(repository.GetAll()).IsDeleted);
        repository.Restore(saved);
        Assert.False(Assert.Single(repository.GetAll()).IsDeleted);

        var settings = new SettingsService();
        settings.Load();
        var viewModel = new MainViewModel(repository, settings);
        var taskInView = Assert.Single(viewModel.PendingTasks);
        viewModel.MoveToStatus(taskInView, WorkStatus.InProgress);
        Assert.Equal(WorkStatus.InProgress, Assert.Single(repository.GetAll()).Status);
        viewModel.MoveToStatus(taskInView, WorkStatus.Completed);
        Assert.NotNull(Assert.Single(repository.GetAll()).CompletedAt);

        var backup = Path.Combine(_tempDirectory, "backup.db");
        repository.BackupTo(backup);
        Assert.True(File.Exists(backup));
        Assert.True(new FileInfo(backup).Length > 0);
    }

    [Fact]
    public void ShortTimerSessionsAreStoredAsSecondsWithoutAddingAMinute()
    {
        var repository = new TaskRepository();
        repository.Initialize();
        var task = new TaskItem { Title = "短时计时" };
        repository.Save(task);

        var startedAt = new DateTime(2026, 9, 17, 10, 0, 0);
        task.ActiveTimerStart = startedAt;
        repository.Save(task);
        repository.StopTimer(task, startedAt.AddSeconds(2));

        Assert.Equal(2, task.ActualSeconds);
        Assert.Equal(0, task.ActualMinutes);
        var restored = Assert.Single(repository.GetAll());
        Assert.Equal(2, restored.ActualSeconds);
        Assert.Equal("2 秒", restored.TimeDisplay);
    }

    [Fact]
    public void CalendarBuildsSixWeeksAndCountsTasksByDate()
    {
        var repository = new TaskRepository();
        repository.Initialize();
        var due = DateTime.Today.AddHours(18);
        repository.Save(new TaskItem { Title = "日历任务", DueAt = due });
        var settings = new SettingsService();
        settings.Load();

        var viewModel = new MainViewModel(repository, settings);
        viewModel.SelectCalendarDate(DateTime.Today);

        Assert.Equal(42, viewModel.CalendarDays.Count);
        var today = Assert.Single(viewModel.CalendarDays.Where(d => d.Date == DateTime.Today));
        Assert.True(today.IsSelected);
        Assert.Equal(1, today.TaskCount);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TASKDOCK_DATA_DIR", null);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory) && _tempDirectory.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            Directory.Delete(_tempDirectory, true);
    }
}
