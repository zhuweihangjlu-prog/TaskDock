using System.Text.Json;
using Microsoft.Data.Sqlite;
using TaskDock.Models;
using TaskDock.Services;

namespace TaskDock.Data;

public sealed class TaskRepository
{
    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = AppPaths.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    public void Initialize()
    {
        AppPaths.EnsureCreated();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS tasks (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                notes TEXT NOT NULL DEFAULT '',
                status INTEGER NOT NULL DEFAULT 0,
                priority INTEGER NOT NULL DEFAULT 1,
                due_at TEXT NULL,
                reminder_at TEXT NULL,
                project TEXT NOT NULL DEFAULT '',
                tags TEXT NOT NULL DEFAULT '',
                subtasks_json TEXT NOT NULL DEFAULT '[]',
                recurrence_rule TEXT NOT NULL DEFAULT '',
                link TEXT NOT NULL DEFAULT '',
                attachment_path TEXT NOT NULL DEFAULT '',
                estimated_minutes INTEGER NOT NULL DEFAULT 0,
                actual_minutes INTEGER NOT NULL DEFAULT 0,
                actual_seconds INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                completed_at TEXT NULL,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                deleted_at TEXT NULL,
                is_archived INTEGER NOT NULL DEFAULT 0,
                last_reminder_at TEXT NULL,
                active_timer_start TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS time_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NOT NULL,
                minutes INTEGER NOT NULL,
                seconds INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(task_id) REFERENCES tasks(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_tasks_due ON tasks(due_at);
            CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status, is_deleted, is_archived);
            CREATE INDEX IF NOT EXISTS idx_tasks_completed ON tasks(completed_at);
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "tasks", "actual_seconds", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "time_entries", "seconds", "INTEGER NOT NULL DEFAULT 0");
        using (var migrate = connection.CreateCommand())
        {
            migrate.CommandText = "UPDATE tasks SET actual_seconds=actual_minutes*60 WHERE actual_seconds=0 AND actual_minutes>0; UPDATE time_entries SET seconds=minutes*60 WHERE seconds=0 AND minutes>0;";
            migrate.ExecuteNonQuery();
        }
        PurgeExpiredTrash();
    }

    public IReadOnlyList<TaskItem> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM tasks ORDER BY is_deleted, status, COALESCE(due_at, '9999'), priority DESC, created_at DESC";
        using var reader = command.ExecuteReader();
        var items = new List<TaskItem>();
        while (reader.Read()) items.Add(Read(reader));
        return items;
    }

    public void Save(TaskItem item)
    {
        item.UpdatedAt = DateTime.Now;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tasks (id,title,notes,status,priority,due_at,reminder_at,project,tags,subtasks_json,recurrence_rule,link,attachment_path,
                estimated_minutes,actual_minutes,actual_seconds,created_at,updated_at,completed_at,is_deleted,deleted_at,is_archived,last_reminder_at,active_timer_start)
            VALUES ($id,$title,$notes,$status,$priority,$due,$reminder,$project,$tags,$subtasks,$recurrence,$link,$attachment,
                $estimated,$actual,$actualSeconds,$created,$updated,$completed,$deleted,$deletedAt,$archived,$lastReminder,$timerStart)
            ON CONFLICT(id) DO UPDATE SET
                title=excluded.title,notes=excluded.notes,status=excluded.status,priority=excluded.priority,due_at=excluded.due_at,
                reminder_at=excluded.reminder_at,project=excluded.project,tags=excluded.tags,subtasks_json=excluded.subtasks_json,
                recurrence_rule=excluded.recurrence_rule,link=excluded.link,attachment_path=excluded.attachment_path,
                estimated_minutes=excluded.estimated_minutes,actual_minutes=excluded.actual_minutes,actual_seconds=excluded.actual_seconds,updated_at=excluded.updated_at,
                completed_at=excluded.completed_at,is_deleted=excluded.is_deleted,deleted_at=excluded.deleted_at,
                is_archived=excluded.is_archived,last_reminder_at=excluded.last_reminder_at,active_timer_start=excluded.active_timer_start;
            """;
        Add(command, "$id", item.Id);
        Add(command, "$title", item.Title);
        Add(command, "$notes", item.Notes);
        Add(command, "$status", (int)item.Status);
        Add(command, "$priority", (int)item.Priority);
        Add(command, "$due", Iso(item.DueAt));
        Add(command, "$reminder", Iso(item.ReminderAt));
        Add(command, "$project", item.Project);
        Add(command, "$tags", item.Tags);
        Add(command, "$subtasks", JsonSerializer.Serialize(item.Subtasks));
        Add(command, "$recurrence", item.RecurrenceRule);
        Add(command, "$link", item.Link);
        Add(command, "$attachment", item.AttachmentPath);
        Add(command, "$estimated", item.EstimatedMinutes);
        Add(command, "$actual", item.ActualMinutes);
        Add(command, "$actualSeconds", item.ActualSeconds);
        Add(command, "$created", Iso(item.CreatedAt));
        Add(command, "$updated", Iso(item.UpdatedAt));
        Add(command, "$completed", Iso(item.CompletedAt));
        Add(command, "$deleted", item.IsDeleted ? 1 : 0);
        Add(command, "$deletedAt", Iso(item.DeletedAt));
        Add(command, "$archived", item.IsArchived ? 1 : 0);
        Add(command, "$lastReminder", Iso(item.LastReminderAt));
        Add(command, "$timerStart", Iso(item.ActiveTimerStart));
        command.ExecuteNonQuery();
    }

    public void SoftDelete(TaskItem item)
    {
        item.IsDeleted = true;
        item.DeletedAt = DateTime.Now;
        Save(item);
    }

    public void Restore(TaskItem item)
    {
        item.IsDeleted = false;
        item.DeletedAt = null;
        Save(item);
    }

    public void DeletePermanently(TaskItem item)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tasks WHERE id=$id";
        Add(command, "$id", item.Id);
        command.ExecuteNonQuery();
    }

    public void PurgeExpiredTrash()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tasks WHERE is_deleted=1 AND deleted_at < $cutoff";
        Add(command, "$cutoff", Iso(DateTime.Now.AddDays(-30)));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<TaskItem> GetDueReminders(DateTime now)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM tasks WHERE is_deleted=0 AND is_archived=0 AND status<>2
            AND reminder_at IS NOT NULL AND reminder_at <= $now
            AND (last_reminder_at IS NULL OR last_reminder_at < reminder_at)
            ORDER BY reminder_at LIMIT 5
            """;
        Add(command, "$now", Iso(now));
        using var reader = command.ExecuteReader();
        var items = new List<TaskItem>();
        while (reader.Read()) items.Add(Read(reader));
        return items;
    }

    public void StopTimer(TaskItem item, DateTime stoppedAt)
    {
        if (!item.ActiveTimerStart.HasValue) return;
        var started = item.ActiveTimerStart.Value;
        var seconds = Math.Max(0, (int)Math.Floor((stoppedAt - started).TotalSeconds));
        var minutes = seconds / 60;
        item.ActualSeconds += seconds;
        item.ActiveTimerStart = null;
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO time_entries(task_id,started_at,ended_at,minutes,seconds) VALUES($task,$start,$end,$minutes,$seconds)";
            Add(command, "$task", item.Id);
            Add(command, "$start", Iso(started));
            Add(command, "$end", Iso(stoppedAt));
            Add(command, "$minutes", minutes);
            Add(command, "$seconds", seconds);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
        Save(item);
    }

    public void BackupTo(string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var source = Open();
        var targetString = new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
        using var target = new SqliteConnection(targetString);
        target.Open();
        source.BackupDatabase(target);
    }

    public void RestoreFrom(string source)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("找不到备份文件。", source);
        SqliteConnection.ClearAllPools();
        File.Copy(source, AppPaths.DatabasePath, true);
        Initialize();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        using var reader = check.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        alter.ExecuteNonQuery();
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static string? Iso(DateTime? value) => value?.ToString("O");

    private static TaskItem Read(SqliteDataReader reader)
    {
        var subtasksJson = reader.GetString(reader.GetOrdinal("subtasks_json"));
        List<SubtaskItem> subtasks;
        try { subtasks = JsonSerializer.Deserialize<List<SubtaskItem>>(subtasksJson) ?? []; } catch { subtasks = []; }
        return new TaskItem
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            Notes = reader.GetString(reader.GetOrdinal("notes")),
            Status = (WorkStatus)reader.GetInt32(reader.GetOrdinal("status")),
            Priority = (TaskPriority)reader.GetInt32(reader.GetOrdinal("priority")),
            DueAt = Date(reader, "due_at"),
            ReminderAt = Date(reader, "reminder_at"),
            Project = reader.GetString(reader.GetOrdinal("project")),
            Tags = reader.GetString(reader.GetOrdinal("tags")),
            Subtasks = subtasks,
            RecurrenceRule = reader.GetString(reader.GetOrdinal("recurrence_rule")),
            Link = reader.GetString(reader.GetOrdinal("link")),
            AttachmentPath = reader.GetString(reader.GetOrdinal("attachment_path")),
            EstimatedMinutes = reader.GetInt32(reader.GetOrdinal("estimated_minutes")),
            ActualSeconds = reader.GetInt32(reader.GetOrdinal("actual_seconds")),
            CreatedAt = Date(reader, "created_at") ?? DateTime.Now,
            UpdatedAt = Date(reader, "updated_at") ?? DateTime.Now,
            CompletedAt = Date(reader, "completed_at"),
            IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
            DeletedAt = Date(reader, "deleted_at"),
            IsArchived = reader.GetInt32(reader.GetOrdinal("is_archived")) == 1,
            LastReminderAt = Date(reader, "last_reminder_at"),
            ActiveTimerStart = Date(reader, "active_timer_start")
        };
    }

    private static DateTime? Date(SqliteDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : DateTime.Parse(reader.GetString(index), null, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}
