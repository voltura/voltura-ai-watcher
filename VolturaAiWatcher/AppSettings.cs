namespace VolturaAiWatcher;

public sealed class AppSettings
{
    public bool StartMinimized { get; set; } = true;
    public bool PlaySoundOnMessage { get; set; }
    public bool ShowClearedMessages { get; set; }
    public System.Collections.Generic.Dictionary<string, long> ClearedThroughUnixMillisecondsByThread { get; set; } =
        new(System.StringComparer.Ordinal);
}
