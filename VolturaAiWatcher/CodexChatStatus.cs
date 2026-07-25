namespace VolturaAiWatcher;

public enum CodexChatStatus
{
    Idle,
    Starting,
    Working,
    WaitingForInput,
    WaitingForApproval,
    WaitingForConnector,
    Completed,
    Interrupted,
    Failed,
    Archived,
    Unknown
}

public static class CodexChatStatusPolicy
{
    public static string GetLabel(CodexChatStatus status) => status switch
    {
        CodexChatStatus.Idle => "IDLE",
        CodexChatStatus.Starting => "STARTING",
        CodexChatStatus.Working => "WORKING",
        CodexChatStatus.WaitingForInput => "WAITING FOR INPUT",
        CodexChatStatus.WaitingForApproval => "WAITING FOR APPROVAL",
        CodexChatStatus.WaitingForConnector => "WAITING FOR APP",
        CodexChatStatus.Completed => "COMPLETED",
        CodexChatStatus.Interrupted => "INTERRUPTED",
        CodexChatStatus.Failed => "FAILED",
        CodexChatStatus.Archived => "ARCHIVED",
        _ => "UNKNOWN"
    };

    public static string GetColor(CodexChatStatus status) => status switch
    {
        CodexChatStatus.Starting => "#9DFFB2",
        CodexChatStatus.Working => "#55FF82",
        CodexChatStatus.WaitingForInput => "#58AFFF",
        CodexChatStatus.WaitingForApproval => "#58AFFF",
        CodexChatStatus.WaitingForConnector => "#7CC8FF",
        CodexChatStatus.Completed => "#58AFFF",
        CodexChatStatus.Interrupted => "#FFB14A",
        CodexChatStatus.Failed => "#FF5874",
        CodexChatStatus.Archived => "#667A6C",
        CodexChatStatus.Unknown => "#D9B85B",
        _ => "#6EA37A"
    };

    public static bool RequiresRetention(CodexChatStatus status) => status is
        CodexChatStatus.Starting or
        CodexChatStatus.Working or
        CodexChatStatus.WaitingForInput or
        CodexChatStatus.WaitingForApproval or
        CodexChatStatus.WaitingForConnector or
        CodexChatStatus.Interrupted or
        CodexChatStatus.Failed or
        CodexChatStatus.Unknown;

    public static bool IsActionable(CodexChatStatus status) => status is
        CodexChatStatus.WaitingForInput or
        CodexChatStatus.WaitingForApproval or
        CodexChatStatus.WaitingForConnector;
}
