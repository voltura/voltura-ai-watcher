namespace VolturaAiWatcher;

public static class NotificationMessagePolicy
{
    public static bool ShouldShow(string sender, bool onlyShowCodexResponses) =>
        !onlyShowCodexResponses || IsCodexResponse(sender);

    public static bool IsCodexResponse(string sender) =>
        string.Equals(sender, "Codex", System.StringComparison.Ordinal);
}
