using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TaskDock.Models;

namespace TaskDock;

public partial class TaskEditorWindow : Window
{
    private readonly TaskItem _item;
    public TaskItem? Result { get; private set; }

    public TaskEditorWindow(TaskItem item)
    {
        InitializeComponent();
        _item = item.Clone();
        TitleBox.Text = _item.Title;
        StatusBox.SelectedIndex = (int)_item.Status;
        PriorityBox.SelectedIndex = (int)_item.Priority;
        DueDate.SelectedDate = _item.DueAt?.Date;
        DueTime.Text = _item.DueAt?.ToString("HH:mm") ?? "18:00";
        ReminderDate.SelectedDate = _item.ReminderAt;
        ProjectBox.Text = _item.Project;
        TagsBox.Text = _item.Tags;
        NotesBox.Text = _item.Notes;
        SubtasksBox.Text = string.Join(Environment.NewLine, _item.Subtasks.Select(s => $"{(s.IsCompleted ? "[x] " : "")}{s.Title}"));
        RecurrenceBox.SelectedIndex = _item.RecurrenceRule switch { "daily" => 1, "weekly" => 2, "monthly" => 3, _ => 0 };
        EstimateBox.Text = _item.EstimatedMinutes > 0 ? _item.EstimatedMinutes.ToString() : string.Empty;
        LinkBox.Text = _item.Link;
        AttachmentBox.Text = _item.AttachmentPath;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "请填写任务标题。", "TaskDock", MessageBoxButton.OK, MessageBoxImage.Information);
            TitleBox.Focus();
            return;
        }
        _item.Title = TitleBox.Text.Trim();
        _item.Status = (WorkStatus)Math.Max(0, StatusBox.SelectedIndex);
        _item.Priority = (TaskPriority)Math.Max(0, PriorityBox.SelectedIndex);
        _item.DueAt = Combine(DueDate.SelectedDate, DueTime.Text);
        _item.ReminderAt = ReminderDate.SelectedDate?.Date.AddHours(_item.DueAt?.Hour ?? 9);
        _item.Project = ProjectBox.Text.Trim();
        _item.Tags = TagsBox.Text.Trim();
        _item.Notes = NotesBox.Text.Trim();
        _item.Subtasks = SubtasksBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => new SubtaskItem { Title = line.Replace("[x]", "", StringComparison.OrdinalIgnoreCase).Trim(), IsCompleted = line.TrimStart().StartsWith("[x]", StringComparison.OrdinalIgnoreCase) }).ToList();
        _item.RecurrenceRule = (RecurrenceBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
        _item.EstimatedMinutes = int.TryParse(EstimateBox.Text, out var estimate) ? Math.Max(0, estimate) : 0;
        _item.Link = LinkBox.Text.Trim();
        _item.AttachmentPath = AttachmentBox.Text.Trim();
        _item.CompletedAt = _item.Status == WorkStatus.Completed ? _item.CompletedAt ?? DateTime.Now : null;
        Result = _item;
        DialogResult = true;
    }

    private static DateTime? Combine(DateTime? date, string time)
    {
        if (!date.HasValue) return null;
        return TimeSpan.TryParse(time, out var parsed) ? date.Value.Date.Add(parsed) : date.Value.Date.AddHours(18);
    }

    private void BrowseAttachment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择本地附件" };
        if (dialog.ShowDialog(this) == true) AttachmentBox.Text = dialog.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
