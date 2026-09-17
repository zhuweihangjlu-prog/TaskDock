using System.Text.RegularExpressions;
using TaskDock.Models;

namespace TaskDock.Services;

public sealed record ParsedTask(string Title, DateTime? DueAt, DateTime? ReminderAt, TaskPriority Priority, string RecurrenceRule);

public static partial class NaturalLanguageParser
{
    public static ParsedTask Parse(string input, DateTime? now = null)
    {
        var reference = now ?? DateTime.Now;
        var title = input.Trim();
        DateTime? date = null;

        if (title.Contains("后天")) date = reference.Date.AddDays(2);
        else if (title.Contains("明天")) date = reference.Date.AddDays(1);
        else if (title.Contains("今天")) date = reference.Date;
        else
        {
            var week = NextWeekdayRegex().Match(title);
            if (week.Success)
            {
                var target = "一二三四五六日天".IndexOf(week.Groups[1].Value[0]);
                if (target == 7) target = 6;
                var current = ((int)reference.DayOfWeek + 6) % 7;
                var days = (target - current + 7) % 7;
                date = reference.Date.AddDays(days == 0 ? 7 : days);
            }
            else
            {
                var md = MonthDayRegex().Match(title);
                if (md.Success && int.TryParse(md.Groups[1].Value, out var month) && int.TryParse(md.Groups[2].Value, out var day))
                {
                    var year = reference.Year;
                    if (new DateTime(year, month, day) < reference.Date) year++;
                    date = new DateTime(year, month, day);
                }
            }
        }

        var tm = ChineseTimeRegex().Match(title);
        var colonTime = ColonTimeRegex().Match(title);
        if (tm.Success || colonTime.Success)
        {
            var hour = tm.Success ? int.Parse(tm.Groups[2].Value) : int.Parse(colonTime.Groups[1].Value);
            var minute = tm.Success && tm.Groups[3].Success ? int.Parse(tm.Groups[3].Value) : colonTime.Success ? int.Parse(colonTime.Groups[2].Value) : 0;
            var period = tm.Success ? tm.Groups[1].Value : string.Empty;
            if ((period is "下午" or "晚上") && hour < 12) hour += 12;
            if (period == "中午" && hour < 11) hour += 12;
            date ??= reference.Date;
            date = date.Value.Date.AddHours(Math.Clamp(hour, 0, 23)).AddMinutes(Math.Clamp(minute, 0, 59));
        }
        else if (date.HasValue) date = date.Value.AddHours(18);

        var priority = title.Contains("紧急") || title.Contains("最高优先") ? TaskPriority.Urgent
            : title.Contains("高优先") || title.Contains("重要") ? TaskPriority.High : TaskPriority.Normal;

        var recurrence = title.Contains("每天") ? "daily" : title.Contains("每周") ? "weekly"
            : title.Contains("每月") ? "monthly" : string.Empty;

        DateTime? reminder = null;
        var reminderMatch = ReminderRegex().Match(title);
        if (date.HasValue && reminderMatch.Success)
        {
            var amount = int.Parse(reminderMatch.Groups[1].Value);
            reminder = reminderMatch.Groups[2].Value switch
            {
                "天" => date.Value.AddDays(-amount),
                "小时" => date.Value.AddHours(-amount),
                _ => date.Value.AddMinutes(-amount)
            };
        }
        else if (date.HasValue && title.Contains("提醒")) reminder = date.Value.AddMinutes(-30);

        title = CleanerRegex().Replace(title, " ");
        title = Regex.Replace(title, @"\s{2,}", " ").Trim(' ', ',', '，');
        if (string.IsNullOrWhiteSpace(title)) title = input.Trim();
        return new ParsedTask(title, date, reminder, priority, recurrence);
    }

    [GeneratedRegex(@"(?:下周|周|星期)([一二三四五六日天])")]
    private static partial Regex NextWeekdayRegex();
    [GeneratedRegex(@"(\d{1,2})月(\d{1,2})[日号]")]
    private static partial Regex MonthDayRegex();
    [GeneratedRegex(@"(上午|中午|下午|晚上)\s*(\d{1,2})(?:[:：点时](\d{1,2})?)?")]
    private static partial Regex ChineseTimeRegex();
    [GeneratedRegex(@"(?<!\d)(\d{1,2})[:：](\d{1,2})(?!\d)")]
    private static partial Regex ColonTimeRegex();
    [GeneratedRegex(@"提前\s*(\d+)\s*(分钟|小时|天)")]
    private static partial Regex ReminderRegex();
    [GeneratedRegex(@"(今天|明天|后天|(?:下周|周|星期)[一二三四五六日天]|\d{1,2}月\d{1,2}[日号]|(?:上午|中午|下午|晚上)\s*\d{1,2}(?:[:：点时]\d{0,2})?|(?<!\d)\d{1,2}[:：]\d{1,2}(?!\d)|提前\s*\d+\s*(?:分钟|小时|天)|提醒|每天|每周|每月|紧急|最高优先|高优先|重要)")]
    private static partial Regex CleanerRegex();
}
