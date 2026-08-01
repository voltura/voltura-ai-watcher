namespace VolturaAiWatcher.Tests;

public sealed class CodexUsageSnapshotTests
{
    [Theory]
    [InlineData(56000, 258400, 22)]
    [InlineData(0, 258400, 0)]
    [InlineData(300000, 258400, 100)]
    public void CalculatesClampedContextPercent(long used, long window, int expected)
    {
        Assert.Equal(expected, VolturaAiWatcher.CodexUsageFormatter.CalculatePercent(used, window));
    }

    [Theory]
    [InlineData(56000, "56k")]
    [InlineData(258400, "258k")]
    [InlineData(999, "999")]
    public void FormatsTokenCounts(long tokens, string expected)
    {
        Assert.Equal(expected, VolturaAiWatcher.CodexUsageFormatter.FormatTokenCount(tokens));
    }

    [Fact]
    public void FormatsModelLabel()
    {
        Assert.Equal(
            "5.6 Luna",
            VolturaAiWatcher.CodexUsageFormatter.FormatModel("gpt-5.6-luna"));
    }

    [Fact]
    public void FormatsWeeklySummaryAndUnknownState()
    {
        var snapshot = new VolturaAiWatcher.CodexUsageSnapshot(
            "gpt-5.6-luna",
            56000,
            258400,
            99,
            System.DateTimeOffset.Parse("2026-08-08T12:00:00Z"),
            System.DateTimeOffset.UtcNow);

        var summary = VolturaAiWatcher.CodexUsageFormatter.FormatWeeklySummary(snapshot);
        Assert.Contains("WEEKLY 99% LEFT", summary);
        Assert.Contains("RESET", summary);
        Assert.Contains("8", summary);
        Assert.Equal(
            "WEEKLY USAGE UNKNOWN",
            VolturaAiWatcher.CodexUsageFormatter.FormatWeeklySummary(null));
    }

    [Fact]
    public void FormatsThreadTooltipWithContextAndModel()
    {
        var snapshot = new VolturaAiWatcher.CodexUsageSnapshot(
            "gpt-5.6-luna",
            56000,
            258400,
            null,
            null,
            System.DateTimeOffset.UtcNow);

        var tooltip = VolturaAiWatcher.CodexUsageFormatter.FormatThreadToolTip(snapshot);

        Assert.NotNull(tooltip);
        Assert.Contains("22% used (78% left)", tooltip);
        Assert.Contains("56k / 258k tokens used", tooltip);
        Assert.Contains("5.6 Luna", tooltip);
    }

    [Fact]
    public void FormatsCompactThreadSummaryForDetailHeader()
    {
        var snapshot = new VolturaAiWatcher.CodexUsageSnapshot(
            "gpt-5.6-luna",
            56000,
            258400,
            null,
            null,
            System.DateTimeOffset.UtcNow);

        Assert.Equal(
            "CONTEXT 22% USED // 56k / 258k // 5.6 LUNA",
            VolturaAiWatcher.CodexUsageFormatter.FormatThreadSummary(snapshot));
        Assert.Equal(
            "CONTEXT USAGE UNKNOWN",
            VolturaAiWatcher.CodexUsageFormatter.FormatThreadSummary(null));
    }

    [Fact]
    public void KeepsOnlyNewerUsageSnapshots()
    {
        var older = new VolturaAiWatcher.CodexUsageSnapshot(
            null,
            100,
            1000,
            null,
            null,
            System.DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var newer = older with { ObservedAt = older.ObservedAt.AddMinutes(1) };

        Assert.False(VolturaAiWatcher.CodexUsagePolicy.IsNewer(newer, older));
        Assert.True(VolturaAiWatcher.CodexUsagePolicy.IsNewer(older, newer));
    }

    [Fact]
    public void MessageEntryExposesWeeklyUsageForNotificationHeaders()
    {
        var snapshot = new VolturaAiWatcher.CodexUsageSnapshot(
            null,
            56000,
            258400,
            99,
            System.DateTimeOffset.Parse("2026-08-08T12:00:00Z"),
            System.DateTimeOffset.UtcNow);
        var entry = new VolturaAiWatcher.CodexMessageEntry
        {
            Id = "id",
            ThreadId = "thread",
            ProjectName = "project",
            Sender = "Codex",
            Text = "message",
            OccurredAt = System.DateTimeOffset.UtcNow,
            WeeklyUsage = snapshot
        };

        Assert.Contains("WEEKLY 99% LEFT", entry.WeeklyUsageText);
        Assert.Contains("Weekly usage remaining: 99%", entry.WeeklyUsageToolTip);
    }
}
