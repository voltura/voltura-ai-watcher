namespace VolturaAiWatcher.Tests;

public sealed class CodexSessionMonitorTests
{
    [Fact]
    public async System.Threading.Tasks.Task CapturesNewMessagesAndLifecycleWithoutReplayingStartupHistory()
    {
        var testRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "VolturaAiWatcherTests",
            System.Guid.NewGuid().ToString("N"));
        var sessions = System.IO.Path.Combine(testRoot, "sessions", "2026", "07", "25");
        System.IO.Directory.CreateDirectory(sessions);
        var threadId = "019f9660-b1ba-7b13-9d9c-80b07599bd16";
        var rollout = System.IO.Path.Combine(
            sessions,
            $"rollout-2026-07-25T01-05-47-{threadId}.jsonl");
        await System.IO.File.WriteAllTextAsync(
            rollout,
            """
            {"timestamp":"2026-07-24T23:05:47.000Z","type":"session_meta","payload":{"session_id":"019f9660-b1ba-7b13-9d9c-80b07599bd16","cwd":"C:\\work\\project-x"}}
            {"timestamp":"2026-07-24T23:05:50.000Z","type":"turn_context","payload":{"model":"gpt-5.6-luna","turn_id":"turn-1"}}
            {"timestamp":"2026-07-24T23:05:51.000Z","type":"event_msg","payload":{"type":"token_count","info":{"model_context_window":258400,"last_token_usage":{"total_tokens":12000}},"rate_limits":{"primary":{"used_percent":1.0,"window_minutes":10080,"resets_at":1786184923}}}}
            {"timestamp":"2026-07-24T23:06:00.000Z","type":"event_msg","payload":{"type":"agent_message","message":"Historical"}}

            """);
        await System.IO.File.WriteAllTextAsync(
            System.IO.Path.Combine(testRoot, "session_index.jsonl"),
            """
            {"id":"019f9660-b1ba-7b13-9d9c-80b07599bd16","thread_name":"Chat title y","updated_at":"2026-07-24T23:06:00Z"}

            """);

        var observed = new System.Collections.Concurrent.ConcurrentQueue<(CodexObservedMessage Message, bool Historical)>();
        var statuses = new System.Collections.Concurrent.ConcurrentQueue<(CodexChatStatus Status, bool Historical)>();
        var usages = new System.Collections.Concurrent.ConcurrentQueue<CodexObservedUsage>();
        using var monitor = new CodexSessionMonitor(testRoot);
        monitor.MessageObserved += (message, historical) => observed.Enqueue((message, historical));
        monitor.StatusObserved += (_, status, _, historical) => statuses.Enqueue((status, historical));
        monitor.UsageObserved += usage => usages.Enqueue(usage);

        await monitor.StartAsync();
        Assert.Contains(observed, item => item.Historical && item.Message.Text == "Historical");
        Assert.Contains(
            usages,
            item => item.Historical &&
                    item.ThreadId == threadId &&
                    item.Usage.ContextTokensUsed == 12000 &&
                    item.Usage.Model == "gpt-5.6-luna");

        await System.IO.File.AppendAllTextAsync(
            rollout,
            """
            {"timestamp":"2026-07-24T23:07:00.000Z","type":"event_msg","payload":{"type":"task_started"}}
            {"timestamp":"2026-07-24T23:07:00.500Z","type":"turn_context","payload":{"model":"gpt-5.6-sol","turn_id":"turn-2"}}
            {"timestamp":"2026-07-24T23:07:00.700Z","type":"event_msg","payload":{"type":"token_count","info":{"model_context_window":258400,"last_token_usage":{"total_tokens":56000}},"rate_limits":{"primary":{"used_percent":2.0,"window_minutes":10080,"resets_at":1786184923}}}}
            {"timestamp":"2026-07-24T23:07:01.000Z","type":"event_msg","payload":{"type":"agent_message","message":"Working on x y z..."}}
            {"timestamp":"2026-07-24T23:07:02.000Z","type":"event_msg","payload":{"type":"task_complete"}}

            """);

        var deadline = System.DateTime.UtcNow.AddSeconds(5);
        while (System.DateTime.UtcNow < deadline &&
               (!observed.Any(item => !item.Historical && item.Message.Text == "Working on x y z...") ||
                !statuses.Any(item => !item.Historical && item.Status == CodexChatStatus.Completed) ||
                !usages.Any(item => !item.Historical && item.Usage.ContextTokensUsed == 56000)))
        {
            await System.Threading.Tasks.Task.Delay(50);
        }

        var newMessage = Assert.Single(
            observed,
            item => !item.Historical && item.Message.Text == "Working on x y z...");
        Assert.Equal("project-x", newMessage.Message.ProjectName);
        Assert.Equal("Chat title y", newMessage.Message.ChatTitle);
        Assert.Contains(
            statuses,
            item => !item.Historical && item.Status == CodexChatStatus.Completed);
        Assert.Contains(
            usages,
            item => !item.Historical &&
                    item.Usage.ContextTokensUsed == 56000 &&
                    item.Usage.Model == "gpt-5.6-sol");

        monitor.Dispose();
        System.IO.Directory.Delete(testRoot, recursive: true);
    }
}
