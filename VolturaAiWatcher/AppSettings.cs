namespace VolturaAiWatcher;

public sealed class AppSettings
{
    public bool StartMinimized { get; set; } = true;
    public bool PlaySoundOnMessage { get; set; }
    public bool OnlyShowCodexResponseNotifications { get; set; }
    public bool ShowClearedMessages { get; set; }
    public int NotificationDurationSeconds { get; set; } = NotificationDurationPolicy.DefaultSeconds;
    public MinimizedMessageClickAction MinimizedMessageClickAction { get; set; } =
        MinimizedMessageClickActionPolicy.Default;
    public System.Collections.Generic.Dictionary<string, long> ClearedThroughUnixMillisecondsByThread { get; set; } =
        new(System.StringComparer.Ordinal);
}
