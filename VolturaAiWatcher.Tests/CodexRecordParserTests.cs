namespace VolturaAiWatcher.Tests;

public sealed class CodexRecordParserTests
{
    [Fact]
    public void ParsesSessionMetadata()
    {
        const string line =
            """
            {"timestamp":"2026-07-24T16:05:21.193Z","type":"session_meta","payload":{"session_id":"019f94df-c3ad-7ef0-88d1-f42c45c15370","cwd":"C:\\work\\project-x"}}
            """;

        Assert.True(CodexRecordParser.TryParse(line, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal("019f94df-c3ad-7ef0-88d1-f42c45c15370", parsed.ThreadId);
        Assert.Equal(@"C:\work\project-x", parsed.WorkingDirectory);
        Assert.Equal(System.DateTimeOffset.Parse("2026-07-24T16:05:21.193Z"), parsed.OccurredAt);
    }

    [Fact]
    public void ParsesTurnContextModel()
    {
        const string line =
            """
            {"timestamp":"2026-07-24T16:05:22.193Z","type":"turn_context","payload":{"model":"gpt-5.6-luna","turn_id":"turn-1"}}
            """;

        Assert.True(CodexRecordParser.TryParse(line, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal("gpt-5.6-luna", parsed.Model);
        Assert.Null(parsed.Usage);
    }

    [Fact]
    public void ParsesTokenCountUsageAndWeeklyRemaining()
    {
        const string line =
            """
            {"timestamp":"2026-07-24T16:05:23.193Z","type":"event_msg","payload":{"type":"token_count","info":{"model_context_window":258400,"last_token_usage":{"total_tokens":56000}},"rate_limits":{"primary":{"used_percent":1.0,"window_minutes":10080,"resets_at":1786184923}}}}
            """;

        Assert.True(CodexRecordParser.TryParse(line, out var parsed));
        Assert.NotNull(parsed);
        Assert.NotNull(parsed.Usage);
        Assert.Equal(56000, parsed.Usage.ContextTokensUsed);
        Assert.Equal(258400, parsed.Usage.ContextWindowTokens);
        Assert.Equal(99, parsed.Usage.WeeklyRemainingPercent);
        Assert.Equal(
            System.DateTimeOffset.FromUnixTimeSeconds(1786184923),
            parsed.Usage.WeeklyResetAt);
    }

    [Fact]
    public void DoesNotTreatNonWeeklyRateLimitAsWeeklyUsage()
    {
        const string line =
            """
            {"timestamp":"2026-07-24T16:05:24.193Z","type":"event_msg","payload":{"type":"token_count","info":{"model_context_window":258400,"last_token_usage":{"total_tokens":56000}},"rate_limits":{"primary":{"used_percent":12.0,"window_minutes":300,"resets_at":1786184923}}}}
            """;

        Assert.True(CodexRecordParser.TryParse(line, out var parsed));
        Assert.NotNull(parsed?.Usage);
        Assert.Null(parsed!.Usage!.WeeklyRemainingPercent);
        Assert.Null(parsed.Usage.WeeklyResetAt);
    }

    [Theory]
    [InlineData("user_message", "You", CodexChatStatus.Starting)]
    [InlineData("agent_message", "Codex", CodexChatStatus.Working)]
    public void ParsesHumanVisibleMessages(string eventType, string sender, CodexChatStatus expectedStatus)
    {
        var line =
            $"{{\"timestamp\":\"2026-07-24T22:51:35.193Z\",\"type\":\"event_msg\",\"payload\":{{\"type\":\"{eventType}\",\"message\":\"Working on x y z...\"}}}}";

        Assert.True(CodexRecordParser.TryParse(line, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(sender, parsed.Sender);
        Assert.Equal("Working on x y z...", parsed.Message);
        Assert.Equal(expectedStatus, parsed.Status);
    }

    [Theory]
    [InlineData("task_started", CodexChatStatus.Working)]
    [InlineData("turn_started", CodexChatStatus.Working)]
    [InlineData("task_complete", CodexChatStatus.Completed)]
    [InlineData("request_user_input", CodexChatStatus.WaitingForInput)]
    [InlineData("exec_approval_request", CodexChatStatus.WaitingForApproval)]
    [InlineData("apply_patch_approval_request", CodexChatStatus.WaitingForApproval)]
    [InlineData("elicitation_request", CodexChatStatus.WaitingForConnector)]
    [InlineData("turn_aborted", CodexChatStatus.Interrupted)]
    [InlineData("error", CodexChatStatus.Failed)]
    public void MapsLifecycleEvents(string eventType, CodexChatStatus expectedStatus)
    {
        var line =
            $"{{\"timestamp\":\"2026-07-24T22:51:35.193Z\",\"type\":\"event_msg\",\"payload\":{{\"type\":\"{eventType}\"}}}}";

        Assert.True(CodexRecordParser.TryParse(line, out var parsed));
        Assert.Equal(expectedStatus, parsed!.Status);
    }

    [Theory]
    [InlineData("agent_reasoning")]
    [InlineData("token_count")]
    [InlineData("mcp_tool_call_end")]
    public void IgnoresInternalOrMalformedEvents(string eventType)
    {
        var line =
            $"{{\"timestamp\":\"2026-07-24T22:51:35.193Z\",\"type\":\"event_msg\",\"payload\":{{\"type\":\"{eventType}\",\"message\":\"private internal data\"}}}}";

        Assert.False(CodexRecordParser.TryParse(line, out _));
    }

    [Fact]
    public void IgnoresMirroredResponseMessages()
    {
        const string line =
            """
            {"timestamp":"2026-07-24T22:51:35.193Z","type":"response_item","payload":{"type":"message","role":"assistant","content":[{"type":"output_text","text":"duplicate"}]}}
            """;

        Assert.False(CodexRecordParser.TryParse(line, out _));
    }

    [Fact]
    public void MessageIdentityIsStableAndContentSensitive()
    {
        var timestamp = System.DateTimeOffset.Parse("2026-07-24T22:51:35.193Z");

        var first = CodexRecordParser.CreateMessageId("thread", timestamp, "Codex", "one");
        var duplicate = CodexRecordParser.CreateMessageId("thread", timestamp, "Codex", "one");
        var different = CodexRecordParser.CreateMessageId("thread", timestamp, "Codex", "two");

        Assert.Equal(first, duplicate);
        Assert.NotEqual(first, different);
    }
}
