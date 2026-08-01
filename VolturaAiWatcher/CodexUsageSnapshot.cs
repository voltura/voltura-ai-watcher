namespace VolturaAiWatcher;

public sealed record CodexUsageSnapshot(
    string? Model,
    long? ContextTokensUsed,
    long? ContextWindowTokens,
    double? WeeklyRemainingPercent,
    System.DateTimeOffset? WeeklyResetAt,
    System.DateTimeOffset ObservedAt);

public static class CodexUsagePolicy
{
    public const int WeeklyWindowMinutes = 7 * 24 * 60;

    public static bool IsNewer(CodexUsageSnapshot? current, CodexUsageSnapshot candidate) =>
        current is null || candidate.ObservedAt >= current.ObservedAt;
}

public static class CodexUsageFormatter
{
    public static string FormatThreadSummary(CodexUsageSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "CONTEXT USAGE UNKNOWN";
        }

        var parts = new System.Collections.Generic.List<string>();
        if (snapshot.ContextTokensUsed is { } used &&
            snapshot.ContextWindowTokens is { } window &&
            window > 0)
        {
            parts.Add($"CONTEXT {CalculatePercent(used, window)}% USED");
            parts.Add($"{FormatTokenCount(used)} / {FormatTokenCount(window)}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Model))
        {
            parts.Add(FormatModel(snapshot.Model).ToUpperInvariant());
        }

        return parts.Count == 0 ? "CONTEXT USAGE UNKNOWN" : string.Join(" // ", parts);
    }

    public static string FormatWeeklySummary(CodexUsageSnapshot? snapshot)
    {
        if (snapshot?.WeeklyRemainingPercent is not { } remaining)
        {
            return "WEEKLY USAGE UNKNOWN";
        }

        var reset = snapshot.WeeklyResetAt is { } resetAt
            ? $" // RESET {resetAt.ToLocalTime().ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"WEEKLY {FormatPercent(remaining)}% LEFT{reset}".ToUpperInvariant();
    }

    public static string FormatWeeklyToolTip(CodexUsageSnapshot? snapshot)
    {
        if (snapshot?.WeeklyRemainingPercent is not { } remaining)
        {
            return "Weekly Codex usage is unavailable from local session telemetry.\nOpen Codex usage for the authoritative account view.";
        }

        var lines = new System.Collections.Generic.List<string>
        {
            $"Weekly usage remaining: {FormatPercent(remaining)}%"
        };
        if (snapshot.WeeklyResetAt is { } resetAt)
        {
            lines.Add($"Resets: {resetAt.ToLocalTime():yyyy-MM-dd HH:mm}");
        }

        lines.Add($"Last known: {FormatAge(snapshot.ObservedAt)}");
        lines.Add("Source: local Codex session telemetry");
        return string.Join("\n", lines);
    }

    public static string? FormatThreadToolTip(CodexUsageSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var lines = new System.Collections.Generic.List<string>();
        if (snapshot.ContextTokensUsed is { } used &&
            snapshot.ContextWindowTokens is { } window &&
            window > 0)
        {
            var usedPercent = CalculatePercent(used, window);
            lines.Add("Context window:");
            lines.Add($"{usedPercent}% used ({100 - usedPercent}% left)");
            lines.Add($"{FormatTokenCount(used)} / {FormatTokenCount(window)} tokens used");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Model))
        {
            lines.Add(FormatModel(snapshot.Model));
        }

        if (lines.Count == 0)
        {
            return null;
        }

        lines.Add($"Last known: {FormatAge(snapshot.ObservedAt)}");
        return string.Join("\n", lines);
    }

    public static int CalculatePercent(long used, long window)
    {
        if (window <= 0)
        {
            return 0;
        }

        var percent = (double)used / window * 100d;
        return System.Math.Clamp(
            (int)System.Math.Round(percent, MidpointRounding.AwayFromZero),
            0,
            100);
    }

    public static string FormatTokenCount(long tokens)
    {
        if (tokens < 1000)
        {
            return tokens.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var thousands = System.Math.Round(tokens / 1000d, MidpointRounding.AwayFromZero);
        return $"{thousands:0}k";
    }

    public static string FormatModel(string model)
    {
        var parts = model
            .Trim()
            .Split('-', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return model.Trim();
        }

        var start = string.Equals(parts[0], "gpt", System.StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;
        if (start >= parts.Length)
        {
            return model.Trim();
        }

        return string.Join(
            " ",
            parts.Skip(start).Select((part, index) =>
                index == 0
                    ? part
                    : char.ToUpperInvariant(part[0]) + part[1..]));
    }

    public static string FormatAge(System.DateTimeOffset observedAt)
    {
        var age = System.DateTimeOffset.UtcNow - observedAt.ToUniversalTime();
        if (age < System.TimeSpan.Zero)
        {
            age = System.TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1)
        {
            return "just now";
        }

        if (age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes}m ago";
        }

        if (age.TotalDays < 1)
        {
            return $"{(int)age.TotalHours}h ago";
        }

        return $"{(int)age.TotalDays}d ago";
    }

    private static int FormatPercent(double value) =>
        System.Math.Clamp(
            (int)System.Math.Round(value, MidpointRounding.AwayFromZero),
            0,
            100);
}
