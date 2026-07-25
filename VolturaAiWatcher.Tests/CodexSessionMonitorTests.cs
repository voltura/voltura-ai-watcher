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
            {"timestamp":"2026-07-24T23:06:00.000Z","type":"event_msg","payload":{"type":"agent_message","message":"Historical"}}

            """);
        await System.IO.File.WriteAllTextAsync(
            System.IO.Path.Combine(testRoot, "session_index.jsonl"),
            """
            {"id":"019f9660-b1ba-7b13-9d9c-80b07599bd16","thread_name":"Chat title y","updated_at":"2026-07-24T23:06:00Z"}

            """);

        var observed = new System.Collections.Concurrent.ConcurrentQueue<(CodexObservedMessage Message, bool Historical)>();
        var statuses = new System.Collections.Concurrent.ConcurrentQueue<(CodexChatStatus Status, bool Historical)>();
        using var monitor = new CodexSessionMonitor(testRoot);
        monitor.MessageObserved += (message, historical) => observed.Enqueue((message, historical));
        monitor.StatusObserved += (_, status, _, historical) => statuses.Enqueue((status, historical));

        await monitor.StartAsync();
        Assert.Contains(observed, item => item.Historical && item.Message.Text == "Historical");

        await System.IO.File.AppendAllTextAsync(
            rollout,
            """
            {"timestamp":"2026-07-24T23:07:00.000Z","type":"event_msg","payload":{"type":"task_started"}}
            {"timestamp":"2026-07-24T23:07:01.000Z","type":"event_msg","payload":{"type":"agent_message","message":"Working on x y z..."}}
            {"timestamp":"2026-07-24T23:07:02.000Z","type":"event_msg","payload":{"type":"task_complete"}}

            """);

        var deadline = System.DateTime.UtcNow.AddSeconds(5);
        while (System.DateTime.UtcNow < deadline &&
               (!observed.Any(item => !item.Historical && item.Message.Text == "Working on x y z...") ||
                !statuses.Any(item => !item.Historical && item.Status == CodexChatStatus.Completed)))
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

        monitor.Dispose();
        System.IO.Directory.Delete(testRoot, recursive: true);
    }
}
