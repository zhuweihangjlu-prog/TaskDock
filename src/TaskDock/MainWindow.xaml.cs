using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using TaskDock.Data;
using TaskDock.Models;
using TaskDock.Services;
using TaskDock.ViewModels;
using Forms = System.Windows.Forms;

namespace TaskDock;

public partial class MainWindow : Window
{
    private const double PeekWidth = 1;
    private readonly TaskRepository _repository;
    private readonly SettingsService _settings;
    private readonly BackupExportService _backupExport;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _edgeTimer;
    private HotkeyService? _hotkey;
    private Forms.Screen _screen = Forms.Screen.PrimaryScreen!;
    private bool _isShown = true;
    private bool _allowClose;
    private bool _initializingSettings;
    private double _dpiScale = 1;
    private Point _calendarDragStart;
    private Point _kanbanDragStart;
    public MainViewModel ViewModel { get; }

    public MainWindow(TaskRepository repository, SettingsService settings, BackupExportService backupExport)
    {
        InitializeComponent();
        _repository = repository;
        _settings = settings;
        _backupExport = backupExport;
        ViewModel = new MainViewModel(repository, settings);
        ViewModel.EditRequested += ViewModel_EditRequested;
        DataContext = ViewModel;
        Width = Math.Clamp(settings.Current.PanelWidth, 390, 800);
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); if (!_settings.Current.IsPinned && !_settings.Current.IsFloating && _settings.Current.AutoHide && !IsMouseOver) HideDock(); };
        _edgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _edgeTimer.Tick += EdgeTimer_Tick;
        Loaded += MainWindow_Loaded;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        _hotkey = new HotkeyService(new WindowInteropHelper(this).Handle, ToggleDock);
        _hotkey.Register(_settings.Current.Hotkey);
        SelectConfiguredScreen();
        PositionDock(false);
        _edgeTimer.Start();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeSettingsControls();
        PinButton.Content = _settings.Current.IsPinned ? "\uE77A" : "\uE718";
    }

    private void InitializeSettingsControls()
    {
        _initializingSettings = true;
        StartWithWindowsBox.IsChecked = _settings.Current.StartWithWindows;
        AutoHideBox.IsChecked = _settings.Current.AutoHide;
        DailyReviewBox.IsChecked = _settings.Current.DailyReviewEnabled;
        DailyReviewTimeBox.Text = _settings.Current.DailyReviewTime;
        HotkeyBox.SelectedIndex = _settings.Current.Hotkey switch { "Ctrl+Shift+Space" => 1, "Win+Alt+T" => 2, _ => 0 };
        ThemeBox.SelectedIndex = _settings.Current.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        StatusFilterBox.SelectedIndex = 0;
        PriorityFilterBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;
        HistoryRangeBox.SelectedIndex = 2;
        DisplayBox.ItemsSource = Forms.Screen.AllScreens.Select((s, i) => new DisplayOption(s.DeviceName, $"显示器 {i + 1} · {s.Bounds.Width}×{s.Bounds.Height}{(s.Primary ? "（主屏）" : "")}")).ToList();
        DisplayBox.SelectedValue = string.IsNullOrWhiteSpace(_settings.Current.DisplayDeviceName) ? Forms.Screen.PrimaryScreen?.DeviceName : _settings.Current.DisplayDeviceName;
        DockEdgeBox.SelectedIndex = _settings.Current.DockEdge switch { "Left" => 1, "Top" => 2, "Bottom" => 3, _ => 0 };
        _initializingSettings = false;
    }

    private void SelectConfiguredScreen()
    {
        _screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _settings.Current.DisplayDeviceName) ?? Forms.Screen.PrimaryScreen!;
    }

    private void PositionDock(bool animate)
    {
        if (_settings.Current.IsFloating) return;
        var area = _screen.WorkingArea;
        var areaLeft = area.Left / _dpiScale;
        var areaTop = area.Top / _dpiScale;
        var areaWidth = area.Width / _dpiScale;
        var areaHeight = area.Height / _dpiScale;
        var edge = NormalizeDockEdge(_settings.Current.DockEdge);
        var isVisible = _isShown || _settings.Current.IsPinned;

        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        if (edge is "Left" or "Right")
        {
            Width = Math.Min(Math.Clamp(_settings.Current.PanelWidth, 390, 800), areaWidth);
            Height = areaHeight;
            MaxHeight = areaHeight;
            Top = areaTop;
            var shownLeft = edge == "Right" ? areaLeft + areaWidth - Width : areaLeft;
            var hiddenLeft = edge == "Right" ? areaLeft + areaWidth - PeekWidth : areaLeft - Width + PeekWidth;
            var target = isVisible ? shownLeft : hiddenLeft;
            if (animate) AnimateTo(LeftProperty, target);
            else Left = target;
            return;
        }

        Width = Math.Min(Math.Max(_settings.Current.PanelWidth, 820), areaWidth);
        Height = Math.Min(areaHeight, Math.Max(MinHeight, Math.Min(720, areaHeight * 0.86)));
        MaxHeight = areaHeight;
        Left = areaLeft + (areaWidth - Width) / 2;
        var shownTop = edge == "Top" ? areaTop : areaTop + areaHeight - Height;
        var hiddenTop = edge == "Top" ? areaTop - Height + PeekWidth : areaTop + areaHeight - PeekWidth;
        var targetTop = isVisible ? shownTop : hiddenTop;
        if (animate) AnimateTo(TopProperty, targetTop);
        else Top = targetTop;
    }

    private void AnimateTo(DependencyProperty property, double target) =>
        BeginAnimation(property, new DoubleAnimation(target, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

    private static string NormalizeDockEdge(string? edge) => edge is "Left" or "Top" or "Bottom" ? edge : "Right";

    private void EdgeTimer_Tick(object? sender, EventArgs e)
    {
        if (_isShown || _settings.Current.IsPinned || _settings.Current.IsFloating) return;
        var cursor = Forms.Cursor.Position;
        var bounds = _screen.Bounds;
        var touchesSelectedEdge = ScreenEdgeDetector.IsAtEdge(cursor.X, cursor.Y, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, _settings.Current.DockEdge);
        if (touchesSelectedEdge) ShowDock();
    }

    public void ShowDock()
    {
        if (!IsVisible) Show();
        _hideTimer.Stop();
        _isShown = true;
        PositionDock(true);
        Activate();
    }

    public void HideDock()
    {
        if (_settings.Current.IsPinned || _settings.Current.IsFloating) return;
        _isShown = false;
        PositionDock(true);
    }

    public void ToggleDock()
    {
        if (_settings.Current.IsFloating) { if (IsVisible && IsActive) Hide(); else { Show(); Activate(); } return; }
        if (_isShown) { _settings.Current.IsPinned = false; PinButton.Content = "\uE718"; HideDock(); }
        else ShowDock();
    }

    public void FocusQuickAdd()
    {
        ViewModel.SelectedView = "Today";
        ShowDock();
        QuickInputBox.Focus();
    }

    public void AllowCloseAndClose() { _allowClose = true; Close(); }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => ShowDock();
    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { if (_settings.Current.AutoHide) _hideTimer.Start(); }

    private void Navigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page }) ViewModel.SelectedView = page;
    }

    private void QuickInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel.AddQuickCommand.CanExecute(null)) { ViewModel.AddQuickCommand.Execute(null); e.Handled = true; }
    }

    private void QuickAddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.AddQuickCommand.CanExecute(null))
        {
            QuickInputBox.Focus();
            ViewModel.ToastMessage = "请先输入任务内容";
            return;
        }

        ViewModel.AddQuickCommand.Execute(null);
    }

    private void NewTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TaskEditorWindow(new TaskItem()) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is not null) ViewModel.SaveEdited(editor.Result);
    }

    private void ViewModel_EditRequested(object? sender, TaskItem item)
    {
        var editor = new TaskEditorWindow(item) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is not null) ViewModel.SaveEdited(editor.Result);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Current.IsPinned = !_settings.Current.IsPinned;
        PinButton.Content = _settings.Current.IsPinned ? "\uE77A" : "\uE718";
        _settings.Save();
        if (_settings.Current.IsPinned) ShowDock();
    }

    private void FloatButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Current.IsFloating = !_settings.Current.IsFloating;
        _settings.Save();
        if (_settings.Current.IsFloating)
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Width = Math.Clamp(_settings.Current.PanelWidth, 390, 800);
            ShowInTaskbar = true; Topmost = false; MaxHeight = double.PositiveInfinity; Height = Math.Min(760, _screen.WorkingArea.Height / _dpiScale - 60);
            Left = _screen.WorkingArea.Left / _dpiScale + 70; Top = _screen.WorkingArea.Top / _dpiScale + 30;
        }
        else { ShowInTaskbar = false; Topmost = true; _isShown = true; PositionDock(false); }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Current.IsFloating) Hide(); else { _settings.Current.IsPinned = false; PinButton.Content = "\uE718"; HideDock(); }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.Current.IsFloating && e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void SettingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingSettings) return;
        if (sender == StartWithWindowsBox) _settings.SetStartWithWindows(StartWithWindowsBox.IsChecked == true);
        else
        {
            _settings.Current.AutoHide = AutoHideBox.IsChecked == true;
            _settings.Current.DailyReviewEnabled = DailyReviewBox.IsChecked == true;
            _settings.Save();
        }
    }

    private void HotkeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || HotkeyBox.SelectedItem is not ComboBoxItem item) return;
        _settings.Current.Hotkey = item.Content.ToString()!;
        _settings.Save();
        if (_hotkey?.Register(_settings.Current.Hotkey) == false) ViewModel.ToastMessage = "快捷键被其他应用占用";
    }

    private void DisplayBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || DisplayBox.SelectedValue is not string device) return;
        _settings.Current.DisplayDeviceName = device; _settings.Save(); SelectConfiguredScreen(); PositionDock(false);
    }

    private void DockEdgeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || DockEdgeBox.SelectedItem is not ComboBoxItem { Tag: string edge }) return;
        _settings.Current.DockEdge = NormalizeDockEdge(edge);
        _settings.Save();
        PositionDock(false);
        ViewModel.ToastMessage = $"已改为从{(edge == "Left" ? "左侧" : edge == "Top" ? "顶部" : edge == "Bottom" ? "底部" : "右侧")}弹出";
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || ThemeBox.SelectedItem is not ComboBoxItem item) return;
        _settings.Current.Theme = item.Tag.ToString()!; _settings.Save(); ThemeService.Apply(_settings.Current.Theme, _settings.Current.Accent);
    }

    private void AccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string accent }) return;
        _settings.Current.Accent = accent;
        _settings.Save();
        ThemeService.Apply(_settings.Current.Theme, accent);
    }

    private void DailyReviewTime_Changed(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (TimeSpan.TryParse(DailyReviewTimeBox.Text, out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1))
        {
            _settings.Current.DailyReviewTime = time.ToString(@"hh\:mm");
            DailyReviewTimeBox.Text = _settings.Current.DailyReviewTime;
            _settings.Save();
        }
        else
        {
            DailyReviewTimeBox.Text = _settings.Current.DailyReviewTime;
            ViewModel.ToastMessage = "请输入有效时间，例如 09:00";
        }
    }

    private void TaskFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings) return;
        if (StatusFilterBox.SelectedItem is ComboBoxItem status) ViewModel.StatusFilter = status.Tag.ToString()!;
        if (PriorityFilterBox.SelectedItem is ComboBoxItem priority) ViewModel.PriorityFilter = priority.Tag.ToString()!;
        if (SortBox.SelectedItem is ComboBoxItem sort) ViewModel.SortMode = sort.Tag.ToString()!;
    }

    private void BatchComplete_Click(object sender, RoutedEventArgs e) => ViewModel.CompleteMany(AllTaskList.SelectedItems.Cast<TaskItem>());
    private void BatchDelete_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteMany(AllTaskList.SelectedItems.Cast<TaskItem>());

    private void HistoryRange_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || HistoryRangeBox.SelectedItem is not ComboBoxItem item) return;
        ViewModel.HistoryRange = item.Tag.ToString()!;
    }

    private void KanbanList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list) _kanbanDragStart = e.GetPosition(list);
    }

    private void KanbanList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox list || e.LeftButton != MouseButtonState.Pressed) return;
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        var position = e.GetPosition(list);
        if (Math.Abs(position.X - _kanbanDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _kanbanDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var container = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (container?.DataContext is TaskItem task) DragDrop.DoDragDrop(container, task, DragDropEffects.Move);
    }

    private void KanbanSection_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskItem)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void KanbanSection_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string statusName }
            || e.Data.GetData(typeof(TaskItem)) is not TaskItem task
            || !Enum.TryParse<WorkStatus>(statusName, out var status)) return;
        ViewModel.MoveToStatus(task, status);
        e.Handled = true;
    }

    private void CalendarTaskList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _calendarDragStart = e.GetPosition(CalendarTaskList);

    private void CalendarTaskList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(CalendarTaskList);
        if (Math.Abs(position.X - _calendarDragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - _calendarDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var container = ItemsControl.ContainerFromElement(CalendarTaskList, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (container?.DataContext is TaskItem task) DragDrop.DoDragDrop(container, task, DragDropEffects.Move);
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e) => ViewModel.ChangeCalendarMonth(-1);
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ViewModel.ChangeCalendarMonth(1);

    private void CalendarDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateTime date }) ViewModel.SelectCalendarDate(date);
    }

    private void CalendarDay_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskItem)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void CalendarDay_Drop(object sender, DragEventArgs e)
    {
        if (sender is Button { Tag: DateTime date } && e.Data.GetData(typeof(TaskItem)) is TaskItem task)
        {
            ViewModel.Reschedule(task, date);
            e.Handled = true;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void PanelWidth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !double.TryParse(value, out var width)) return;
        Width = width; _settings.Current.PanelWidth = width; _settings.Save(); PositionDock(false);
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var path = _backupExport.CreateAutomaticBackup();
        ViewModel.ToastMessage = $"备份已保存：{Path.GetFileName(path)}";
    }

    private void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择 TaskDock 数据库备份", Filter = "TaskDock 备份|*.db|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        if (System.Windows.MessageBox.Show(this, "恢复备份会替换当前任务数据。TaskDock 会先自动备份当前数据，是否继续？", "恢复备份", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _backupExport.CreateAutomaticBackup();
            _repository.RestoreFrom(dialog.FileName);
            ViewModel.Reload();
            ViewModel.ToastMessage = "备份恢复完成";
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "导入任务数据", Filter = "支持的格式|*.json;*.csv;*.md;*.markdown|JSON|*.json|CSV|*.csv|Markdown|*.md;*.markdown" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var count = _backupExport.Import(dialog.FileName);
            ViewModel.Reload();
            ViewModel.ToastMessage = $"已导入 {count} 项任务";
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string format }) return;
        var dialog = new SaveFileDialog { FileName = $"TaskDock-{DateTime.Now:yyyyMMdd}", Filter = format switch { "json" => "JSON 文件|*.json", "csv" => "CSV 文件|*.csv", _ => "Markdown 文件|*.md" }, DefaultExt = format };
        if (dialog.ShowDialog(this) != true) return;
        var tasks = _repository.GetAll().Where(t => !t.IsDeleted);
        if (format == "json") _backupExport.ExportJson(dialog.FileName, tasks);
        else if (format == "csv") _backupExport.ExportCsv(dialog.FileName, tasks);
        else _backupExport.ExportMarkdown(dialog.FileName, tasks);
        ViewModel.ToastMessage = "导出完成";
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.DataDirectory) { UseShellExecute = true });

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose) { e.Cancel = true; if (_settings.Current.IsFloating) Hide(); else HideDock(); return; }
        if (_settings.Current.IsFloating || NormalizeDockEdge(_settings.Current.DockEdge) is "Left" or "Right")
            _settings.Current.PanelWidth = Width;
        _settings.Save(); _hotkey?.Dispose();
        _edgeTimer.Stop();
    }
}
