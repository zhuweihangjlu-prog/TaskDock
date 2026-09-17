using System.Globalization;
using System.Text;
using System.Text.Json;
using TaskDock.Data;
using TaskDock.Models;

namespace TaskDock.Services;

public sealed class BackupExportService(TaskRepository repository)
{
    public string CreateAutomaticBackup()
    {
        AppPaths.EnsureCreated();
        var path = Path.Combine(AppPaths.BackupDirectory, $"TaskDock-{DateTime.Now:yyyyMMdd-HHmm}.db");
        repository.BackupTo(path);
        foreach (var old in Directory.GetFiles(AppPaths.BackupDirectory, "TaskDock-*.db").OrderByDescending(File.GetCreationTime).Skip(14))
            File.Delete(old);
        return path;
    }

    public void ExportJson(string path, IEnumerable<TaskItem> tasks) =>
        File.WriteAllText(path, JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

    public void ExportMarkdown(string path, IEnumerable<TaskItem> tasks)
    {
        var builder = new StringBuilder("# TaskDock 工作记录\n\n");
        foreach (var group in tasks.OrderByDescending(t => t.CompletedAt ?? t.DueAt ?? t.CreatedAt).GroupBy(t => (t.CompletedAt ?? t.DueAt ?? t.CreatedAt).Date))
        {
            builder.AppendLine($"## {group.Key:yyyy年M月d日}").AppendLine();
            foreach (var task in group) builder.AppendLine($"- [{(task.Status == WorkStatus.Completed ? 'x' : ' ')}] {task.Title}" +
                (string.IsNullOrWhiteSpace(task.Project) ? "" : $" · {task.Project}"));
            builder.AppendLine();
        }
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    public void ExportCsv(string path, IEnumerable<TaskItem> tasks)
    {
        static string Q(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
        var lines = new List<string> { "标题,状态,优先级,截止时间,项目,标签,实际耗时(分钟),完成时间,备注" };
        lines.AddRange(tasks.Select(t => string.Join(',', Q(t.Title), Q(t.Status.ToString()), Q(t.Priority.ToString()),
            Q(t.DueAt?.ToString("O", CultureInfo.InvariantCulture)), Q(t.Project), Q(t.Tags), t.ActualMinutes,
            Q(t.CompletedAt?.ToString("O", CultureInfo.InvariantCulture)), Q(t.Notes))));
        File.WriteAllLines(path, lines, new UTF8Encoding(true));
    }

    public int Import(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var tasks = extension switch
        {
            ".json" => ImportJson(path),
            ".csv" => ImportCsv(path),
            ".md" or ".markdown" => ImportMarkdown(path),
            _ => throw new InvalidDataException("不支持的导入格式。")
        };
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Id)) task.Id = Guid.NewGuid().ToString("N");
            task.UpdatedAt = DateTime.Now;
            repository.Save(task);
        }
        return tasks.Count;
    }

    private static List<TaskItem> ImportJson(string path) =>
        JsonSerializer.Deserialize<List<TaskItem>>(File.ReadAllText(path, Encoding.UTF8)) ?? [];

    private static List<TaskItem> ImportMarkdown(string path)
    {
        var result = new List<TaskItem>();
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("- [", StringComparison.Ordinal) || trimmed.Length < 6) continue;
            var completed = trimmed.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase);
            result.Add(new TaskItem { Title = trimmed[5..].Trim(), Status = completed ? WorkStatus.Completed : WorkStatus.Pending, CompletedAt = completed ? DateTime.Now : null });
        }
        return result;
    }

    private static List<TaskItem> ImportCsv(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var result = new List<TaskItem>();
        foreach (var line in lines.Skip(1))
        {
            var fields = ParseCsvLine(line);
            if (fields.Count == 0 || string.IsNullOrWhiteSpace(fields[0])) continue;
            var task = new TaskItem { Title = fields[0] };
            if (fields.Count > 1 && Enum.TryParse<WorkStatus>(fields[1], true, out var status)) task.Status = status;
            if (fields.Count > 2 && Enum.TryParse<TaskPriority>(fields[2], true, out var priority)) task.Priority = priority;
            if (fields.Count > 3 && DateTime.TryParse(fields[3], out var due)) task.DueAt = due;
            if (fields.Count > 4) task.Project = fields[4];
            if (fields.Count > 5) task.Tags = fields[5];
            if (fields.Count > 6 && int.TryParse(fields[6], out var minutes)) task.ActualMinutes = minutes;
            if (fields.Count > 7 && DateTime.TryParse(fields[7], out var completed)) task.CompletedAt = completed;
            if (fields.Count > 8) task.Notes = fields[8];
            result.Add(task);
        }
        return result;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }
}
