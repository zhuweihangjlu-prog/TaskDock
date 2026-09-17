using System.Windows;
using System.Windows.Threading;
using TaskDock.Data;
using TaskDock.Services;
using Forms = System.Windows.Forms;

namespace TaskDock;

public partial class App : System.Windows.Application
{
    private readonly System.Threading.Mutex _singleInstance;
    private readonly bool _ownsSingleInstance;
    private SettingsService? _settings;
    private TaskRepository? _repository;
    private BackupExportService? _backup;
    private MainWindow? _mainWindow;
    private Forms.NotifyIcon? _trayIcon;
    private DispatcherTimer? _reminderTimer;
    private DateTime? _dailyReviewShown;

    public App()
    {
#if DEBUG
        const string mutexName = "TaskDock.Local.SingleInstance.Debug";
#else
        const string mutexName = "TaskDock.Local.SingleInstance";
#endif
        _singleInstance = new System.Threading.Mutex(true, mutexName, out _ownsSingleInstance);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!_ownsSingleInstance)
        {
            System.Windows.MessageBox.Show("TaskDock 已经在运行，请使用托盘图标或全局快捷键呼出。", "TaskDock", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnUnhandledException;

        _settings = new SettingsService();
        _settings.Load();
        ThemeService.Apply(_settings.Current.Theme, _settings.Current.Accent);
        _repository = new TaskRepository();
        _repository.Initialize();
        _backup = new BackupExportService(_repository);

#if DEBUG
        if (e.Args.Contains("--qa-seed")) SeedQaData();
#endif

        TryAutomaticBackup();
        _mainWindow = new MainWindow(_repository, _settings, _backup);
        MainWindow = _mainWindow;
        CreateTrayIcon();
        StartReminderTimer();
        _mainWindow.Show();

        if (e.Args.Contains("--background"))
            Dispatcher.BeginInvoke(() => _mainWindow.HideDock(), DispatcherPriority.ApplicationIdle);
        else
            _mainWindow.ShowDock();

        if (_settings.Current.FirstRun)
            Dispatcher.BeginInvoke(ShowFirstRunPrompt, DispatcherPriority.ContextIdle);
    }

#if DEBUG
    private void SeedQaData()
    {
        if (_settings is null || _repository is null) return;
        _settings.Current.FirstRun = false;
        _settings.Current.Theme = "Dark";
        _settings.Current.AutoHide = false;
        _settings.Save();
        if (_repository.GetAll().Any()) return;
        _repository.Save(new Models.TaskItem { Title = "整理本周工作计划", Project = "日常工作", Tags = "计划", Status = Models.WorkStatus.Pending, DueAt = DateTime.Today.AddHours(18) });
        _repository.Save(new Models.TaskItem { Title = "完成 TaskDock 深色模式检查", Project = "TaskDock", Tags = "开发,界面", Status = Models.WorkStatus.InProgress, Priority = Models.TaskPriority.High, DueAt = DateTime.Today.AddHours(16) });
        _repository.Save(new Models.TaskItem { Title = "确认本地备份可用", Project = "TaskDock", Status = Models.WorkStatus.Completed, CompletedAt = DateTime.Now.AddHours(-1), ActualMinutes = 25 });
    }
#endif

    private void ShowFirstRunPrompt()
    {
        if (_settings is null || _mainWindow is null) return;
        var answer = System.Windows.MessageBox.Show(_mainWindow, "欢迎使用 TaskDock！\n\n是否希望 TaskDock 随 Windows 自动启动？你之后可以在设置中更改。", "TaskDock", MessageBoxButton.YesNo, MessageBoxImage.Question);
        _settings.Current.FirstRun = false;
        _settings.SetStartWithWindows(answer == MessageBoxResult.Yes);
    }

    private void CreateTrayIcon()
    {
        var icon = Environment.ProcessPath is null ? System.Drawing.SystemIcons.Application : System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? System.Drawing.SystemIcons.Application;
        _trayIcon = new Forms.NotifyIcon { Text = "TaskDock · 本地待办", Icon = icon, Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("快速添加任务", null, (_, _) => Dispatcher.Invoke(() => _mainWindow?.FocusQuickAdd()));
        menu.Items.Add("打开 TaskDock", null, (_, _) => Dispatcher.Invoke(() => _mainWindow?.ShowDock()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => _mainWindow?.ToggleDock());
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.Invoke(() => _mainWindow?.ShowDock());
    }

    private void StartReminderTimer()
    {
        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _reminderTimer.Tick += (_, _) => CheckReminders();
        _reminderTimer.Start();
        CheckReminders();
    }

    private void CheckReminders()
    {
        if (_repository is null || _settings is null || _trayIcon is null) return;
        foreach (var task in _repository.GetDueReminders(DateTime.Now))
        {
            _trayIcon.ShowBalloonTip(7000, "TaskDock 提醒", task.Title, Forms.ToolTipIcon.Info);
            task.LastReminderAt = DateTime.Now;
            _repository.Save(task);
        }

        if (_settings.Current.DailyReviewEnabled && TimeSpan.TryParse(_settings.Current.DailyReviewTime, out var reviewTime)
            && DateTime.Now.TimeOfDay >= reviewTime && _dailyReviewShown != DateTime.Today)
        {
            _dailyReviewShown = DateTime.Today;
            var count = _repository.GetAll().Count(t => !t.IsDeleted && !t.IsArchived && t.Status != Models.WorkStatus.Completed);
            _trayIcon.ShowBalloonTip(7000, "今日任务", count == 0 ? "今天暂时没有待办，轻松开始吧。" : $"当前还有 {count} 项工作等待处理。", Forms.ToolTipIcon.Info);
        }
    }

    private void TryAutomaticBackup()
    {
        if (_settings is null || _backup is null || _settings.Current.LastAutomaticBackup?.Date == DateTime.Today) return;
        try
        {
            _backup.CreateAutomaticBackup();
            _settings.Current.LastAutomaticBackup = DateTime.Now;
            _settings.Save();
        }
        catch { }
    }

    private void ExitApplication()
    {
        _reminderTimer?.Stop();
        if (_trayIcon is not null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
        _mainWindow?.AllowCloseAndClose();
        _singleInstance.Dispose();
        Shutdown();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _mainWindow?.AllowCloseAndClose();
        base.OnSessionEnding(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            AppPaths.EnsureCreated();
            File.AppendAllText(Path.Combine(AppPaths.DataDirectory, "taskdock-error.log"), $"[{DateTime.Now:O}] {e.Exception}\n\n");
        }
        catch { }
        System.Windows.MessageBox.Show($"TaskDock 遇到了问题，错误日志已保存在本地数据目录。\n\n{e.Exception.Message}", "TaskDock", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
