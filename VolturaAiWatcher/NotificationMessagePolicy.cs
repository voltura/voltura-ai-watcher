namespace VolturaAiWatcher;

public static class NotificationMessagePolicy
{
    public static bool ShouldShow(
        bool monitoringPaused,
        string sender,
        bool onlyShowCodexResponses) =>
        !monitoringPaused && (!onlyShowCodexResponses || IsCodexResponse(sender));

    public static bool IsCodexResponse(string sender) =>
        string.Equals(sender, "Codex", System.StringComparison.Ordinal);
}
