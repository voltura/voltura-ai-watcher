namespace VolturaAiWatcher;

public static class NotificationDurationPolicy
{
    public const int UntilDismissed = -1;
    public const int Off = 0;
    public const int MinimumSeconds = 1;
    public const int MaximumSeconds = 600;
    public const int DefaultSeconds = 10;

    public static int NormalizePersisted(int seconds) => seconds is UntilDismissed or Off
        || seconds is >= MinimumSeconds and <= MaximumSeconds
            ? seconds
            : DefaultSeconds;

    public static int NormalizeCustom(int seconds) =>
        System.Math.Clamp(seconds, MinimumSeconds, MaximumSeconds);

    public static bool IsPreset(int seconds) => seconds is Off or 5 or 10 or UntilDismissed;
}
