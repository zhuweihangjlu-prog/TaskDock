using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TaskDock.Models;
using TaskDock.Services;

namespace TaskDock;

public partial class TaskEditorWindow : Window
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";
    private readonly TaskItem _item;
    private readonly ObservableCollection<SubtaskItem> _subtasks = [];

    public TaskItem? Result { get; private set; }

    public TaskEditorWindow(TaskItem item)
    {
        InitializeComponent();
        _item = item.Clone();
        foreach (var subtask in _item.Subtasks)
            _subtasks.Add(new SubtaskItem { Id = subtask.Id, Title = subtask.Title, IsCompleted = subtask.IsCompleted });

        TitleBox.Text = _item.Title;
        StatusBox.SelectedIndex = (int)_item.Status;
        PriorityBox.SelectedIndex = (int)_item.Priority;
        SetDateTime(DueDateBox, DueTimeBox, _item.DueAt, "18:00");
        SetDateTime(ReminderDateBox, ReminderTimeBox, _item.ReminderAt, "09:00");
        ProjectBox.Text = _item.Project;
        TagsBox.Text = _item.Tags;
        NotesBox.Text = _item.Notes;
        SubtasksList.ItemsSource = _subtasks;
        RecurrenceBox.SelectedIndex = _item.RecurrenceRule switch { "daily" => 1, "weekly" => 2, "monthly" => 3, _ => 0 };
        EstimateBox.Text = _item.EstimatedMinutes > 0 ? _item.EstimatedMinutes.ToString(CultureInfo.InvariantCulture) : string.Empty;
        LinkBox.Text = _item.Link;
        AttachmentBox.Text = _item.AttachmentPath;
        UpdateResourceButtons();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            ShowValidation("请填写任务标题。", TitleBox);
            return;
        }

        if (!TryReadDateTime(DueDateBox, DueTimeBox, "截止时间", out var dueAt)) return;
        if (!TryReadDateTime(ReminderDateBox, ReminderTimeBox, "提醒时间", out var reminderAt)) return;
        if (!string.IsNullOrWhiteSpace(LinkBox.Text) && ResourceLauncher.NormalizeWebUri(LinkBox.Text) is null)
        {
            ShowValidation("网页链接格式不正确，请输入有效的网址。", LinkBox);
            return;
        }

        _item.Title = TitleBox.Text.Trim();
        _item.Status = (WorkStatus)Math.Max(0, StatusBox.SelectedIndex);
        _item.Priority = (TaskPriority)Math.Max(0, PriorityBox.SelectedIndex);
        _item.DueAt = dueAt;
        _item.ReminderAt = reminderAt;
        _item.Project = ProjectBox.Text.Trim();
        _item.Tags = TagsBox.Text.Trim();
        _item.Notes = NotesBox.Text.Trim();
        _item.Subtasks = _subtasks
            .Where(s => !string.IsNullOrWhiteSpace(s.Title))
            .Select(s => new SubtaskItem { Id = s.Id, Title = s.Title.Trim(), IsCompleted = s.IsCompleted })
            .ToList();
        _item.RecurrenceRule = (RecurrenceBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
        _item.EstimatedMinutes = int.TryParse(EstimateBox.Text, out var estimate) ? Math.Max(0, estimate) : 0;
        _item.Link = LinkBox.Text.Trim();
        _item.AttachmentPath = AttachmentBox.Text.Trim();
        _item.CompletedAt = _item.Status == WorkStatus.Completed ? _item.CompletedAt ?? DateTime.Now : null;
        Result = _item;
        DialogResult = true;
    }

    private static void SetDateTime(TextBox dateBox, TextBox timeBox, DateTime? value, string defaultTime)
    {
        dateBox.Text = value?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;
        timeBox.Text = value?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? defaultTime;
    }

    private bool TryReadDateTime(TextBox dateBox, TextBox timeBox, string fieldName, out DateTime? value)
    {
        value = null;
        var dateText = dateBox.Text.Trim();
        var timeText = timeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(dateText))
        {
            if (!string.IsNullOrWhiteSpace(timeText) && timeText is not "18:00" and not "09:00")
            {
                ShowValidation($"请先填写{fieldName}的日期。", dateBox);
                return false;
            }
            return true;
        }

        if (!DateTime.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            ShowValidation($"{fieldName}的日期格式应为 yyyy-MM-dd，例如 {DateTime.Today:yyyy-MM-dd}。", dateBox);
            return false;
        }
        if (!TimeSpan.TryParseExact(timeText, @"hh\:mm", CultureInfo.InvariantCulture, out var time)
            || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
        {
            ShowValidation($"{fieldName}的时间格式应为 HH:mm，例如 18:30。", timeBox);
            return false;
        }
        value = date.Date.Add(time);
        return true;
    }

    private void DueToday_Click(object sender, RoutedEventArgs e) => SetDateTime(DueDateBox, DueTimeBox, DateTime.Today.AddHours(18), "18:00");
    private void DueTomorrow_Click(object sender, RoutedEventArgs e) => SetDateTime(DueDateBox, DueTimeBox, DateTime.Today.AddDays(1).AddHours(18), "18:00");
    private void ClearDue_Click(object sender, RoutedEventArgs e) => SetDateTime(DueDateBox, DueTimeBox, null, "18:00");

    private void Reminder15_Click(object sender, RoutedEventArgs e) => SetReminderBeforeDue(TimeSpan.FromMinutes(15));
    private void Reminder60_Click(object sender, RoutedEventArgs e) => SetReminderBeforeDue(TimeSpan.FromHours(1));
    private void ClearReminder_Click(object sender, RoutedEventArgs e) => SetDateTime(ReminderDateBox, ReminderTimeBox, null, "09:00");

    private void SetReminderBeforeDue(TimeSpan offset)
    {
        if (!TryReadDateTime(DueDateBox, DueTimeBox, "截止时间", out var dueAt) || !dueAt.HasValue)
        {
            ShowValidation("请先设置截止日期和时间，再使用相对提醒。", DueDateBox);
            return;
        }
        SetDateTime(ReminderDateBox, ReminderTimeBox, dueAt.Value.Subtract(offset), "09:00");
    }

    private void AddSubtask_Click(object sender, RoutedEventArgs e) => AddSubtask();

    private void NewSubtask_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddSubtask();
        e.Handled = true;
    }

    private void AddSubtask()
    {
        var title = NewSubtaskBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            NewSubtaskBox.Focus();
            return;
        }
        _subtasks.Add(new SubtaskItem { Title = title });
        NewSubtaskBox.Clear();
        NewSubtaskBox.Focus();
    }

    private void RemoveSubtask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: SubtaskItem subtask }) _subtasks.Remove(subtask);
    }

    private void BrowseAttachment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择本地附件", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) AttachmentBox.Text = dialog.FileName;
    }

    private void OpenNotesLink_Click(object sender, RoutedEventArgs e)
    {
        if (!TryLaunch(() => ResourceLauncher.OpenWeb(NotesBox.Text, true), "备注中没有找到可打开的 http:// 或 https:// 网页地址。"))
            NotesBox.Focus();
    }

    private void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (!TryLaunch(() => ResourceLauncher.OpenWeb(LinkBox.Text), "请先填写有效的网页链接。")) LinkBox.Focus();
    }

    private void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (!TryLaunch(() => ResourceLauncher.OpenFile(AttachmentBox.Text), "附件不存在或已被移动，请重新选择文件。")) AttachmentBox.Focus();
    }

    private bool TryLaunch(Func<bool> launch, string unavailableMessage)
    {
        try
        {
            if (launch()) return true;
            MessageBox.Show(this, unavailableMessage, "TaskDock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法打开：{ex.Message}", "TaskDock", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return false;
    }

    private void ResourceText_Changed(object sender, TextChangedEventArgs e) => UpdateResourceButtons();

    private void UpdateResourceButtons()
    {
        if (!IsInitialized) return;
        OpenNotesLinkButton.IsEnabled = ResourceLauncher.FindWebUri(NotesBox.Text) is not null;
        OpenLinkButton.IsEnabled = ResourceLauncher.NormalizeWebUri(LinkBox.Text) is not null;
        OpenAttachmentButton.IsEnabled = File.Exists(AttachmentBox.Text.Trim());
    }

    private void ShowValidation(string message, Control control)
    {
        MessageBox.Show(this, message, "TaskDock", MessageBoxButton.OK, MessageBoxImage.Information);
        control.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
