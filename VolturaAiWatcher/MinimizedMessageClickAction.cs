namespace VolturaAiWatcher;

public enum MinimizedMessageClickAction
{
    ShowMessageDetails,
    OpenInCodex
}

public static class MinimizedMessageClickActionPolicy
{
    public const MinimizedMessageClickAction Default = MinimizedMessageClickAction.ShowMessageDetails;

    public static MinimizedMessageClickAction NormalizePersisted(MinimizedMessageClickAction action) =>
        action is MinimizedMessageClickAction.ShowMessageDetails or MinimizedMessageClickAction.OpenInCodex
            ? action
            : Default;
}
