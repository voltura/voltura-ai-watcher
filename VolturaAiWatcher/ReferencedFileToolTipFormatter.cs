namespace VolturaAiWatcher;

public static class ReferencedFileToolTipFormatter
{
    public static string FormatAutomaticOpen(ReferencedFileResolution? reference)
    {
        if (reference is null)
        {
            return "Open referenced file";
        }

        var reason = reference.AvailableFileCount == 1
            ? "only available file reference"
            : $"first available file reference ({reference.SelectionIndex} of {reference.AvailableFileCount})";
        return
            $"Open {System.IO.Path.GetFileName(reference.Path)}\n" +
            $"{reference.Path}\n" +
            $"Source: {FormatSection(reference.MessageSection)} · {FormatSourceKind(reference.SourceKind)}\n" +
            $"Why: {reason}";
    }

    public static string FormatDirectOpen(string path) =>
        $"Open {System.IO.Path.GetFileName(path)}\n{path}\nSource: direct link in message";

    private static string FormatSection(ReferencedFileMessageSection section) =>
        section switch
        {
            ReferencedFileMessageSection.ApprovalContext => "approval context",
            ReferencedFileMessageSection.ApprovalTranscript => "approval transcript",
            ReferencedFileMessageSection.ApprovalPlannedAction => "planned approval action",
            _ => "message body"
        };

    private static string FormatSourceKind(ReferencedFileSourceKind sourceKind) =>
        sourceKind switch
        {
            ReferencedFileSourceKind.MarkdownLink => "Markdown link",
            ReferencedFileSourceKind.InlineCode => "inline code",
            _ => "absolute path"
        };
}
