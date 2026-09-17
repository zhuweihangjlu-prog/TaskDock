using TaskDock.Models;
using TaskDock.Services;

namespace TaskDock.Tests;

public sealed class NaturalLanguageParserTests
{
    private static readonly DateTime Reference = new(2026, 9, 16, 10, 0, 0);

    [Fact]
    public void ParsesChineseDateTimeReminderAndPriority()
    {
        var result = NaturalLanguageParser.Parse("明天下午3点提交周报，提前30分钟提醒，重要", Reference);

        Assert.Equal(new DateTime(2026, 9, 17, 15, 0, 0), result.DueAt);
        Assert.Equal(new DateTime(2026, 9, 17, 14, 30, 0), result.ReminderAt);
        Assert.Equal(TaskPriority.High, result.Priority);
        Assert.Contains("提交周报", result.Title);
    }

    [Fact]
    public void ParsesNextWeekdayAndRecurrence()
    {
        var result = NaturalLanguageParser.Parse("下周一上午9点例会 每周", Reference);

        Assert.Equal(new DateTime(2026, 9, 21, 9, 0, 0), result.DueAt);
        Assert.Equal("weekly", result.RecurrenceRule);
    }

    [Fact]
    public void DoesNotTreatOrdinaryNumberAsTime()
    {
        var result = NaturalLanguageParser.Parse("整理2份项目材料", Reference);
        Assert.Null(result.DueAt);
    }

    [Fact]
    public void DetectsOnlyTheSelectedScreensRightEdge()
    {
        Assert.True(ScreenEdgeDetector.IsAtRightEdge(1919, 500, 0, 0, 1920, 1080));
        Assert.True(ScreenEdgeDetector.IsAtRightEdge(1917, 0, 0, 0, 1920, 1080));
        Assert.False(ScreenEdgeDetector.IsAtRightEdge(1916, 500, 0, 0, 1920, 1080));
        Assert.False(ScreenEdgeDetector.IsAtRightEdge(1919, 1080, 0, 0, 1920, 1080));
        Assert.False(ScreenEdgeDetector.IsAtRightEdge(2559, 500, 0, 0, 1920, 1080));
    }
}
