namespace VolturaAiWatcher;

public enum ReferencedFileSourceKind
{
    MarkdownLink,
    InlineCode,
    AbsolutePath
}

public enum ReferencedFileMessageSection
{
    MessageBody,
    ApprovalContext,
    ApprovalTranscript,
    ApprovalPlannedAction
}

public sealed record ReferencedFileResolution(
    string Path,
    ReferencedFileSourceKind SourceKind,
    ReferencedFileMessageSection MessageSection,
    int SelectionIndex,
    int AvailableFileCount);

public static class ReferencedFileResolver
{
    private const string TranscriptStartMarker = ">>> TRANSCRIPT START";
    private const string TranscriptDeltaStartMarker = ">>> TRANSCRIPT DELTA START";
    private const string TranscriptEndMarker = ">>> TRANSCRIPT END";
    private const string TranscriptDeltaEndMarker = ">>> TRANSCRIPT DELTA END";
    private const string ApprovalStartMarker = ">>> APPROVAL REQUEST START";

    private static readonly System.Text.RegularExpressions.Regex MarkdownTargetPattern = new(
        @"\]\((?<value><[^>\r\n]+>|[^)\r\n]+)\)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex InlineCodePattern = new(
        @"(?<!`)`(?<value>[^`\r\n]+)`(?!`)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex AbsoluteWindowsPathPattern = new(
        @"(?<value>[A-Za-z]:[\\/][^\r\n<>|""*?]+)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex LineSuffixPattern = new(
        @":\d+(?::\d+)?$",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public static string? ResolveFirstExistingFile(string? message, string? workingDirectory) =>
        ResolveFirstExistingFileReference(message, workingDirectory)?.Path;

    public static ReferencedFileResolution? ResolveFirstExistingFileReference(
        string? message,
        string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var candidates = new System.Collections.Generic.List<FileReferenceCandidate>();
        AddMatches(candidates, MarkdownTargetPattern, ReferencedFileSourceKind.MarkdownLink, message);
        AddMatches(candidates, InlineCodePattern, ReferencedFileSourceKind.InlineCode, message);
        AddMatches(candidates, AbsoluteWindowsPathPattern, ReferencedFileSourceKind.AbsolutePath, message);

        var existing = new System.Collections.Generic.List<ResolvedCandidate>();
        var seenReferences = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase);
        var seenPaths = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!seenReferences.Add(candidate.Value))
            {
                continue;
            }

            var resolved = TryResolve(candidate.Value, workingDirectory);
            if (resolved is not null &&
                System.IO.File.Exists(resolved) &&
                seenPaths.Add(resolved))
            {
                existing.Add(new ResolvedCandidate(candidate, resolved));
            }
        }

        if (existing.Count == 0)
        {
            return null;
        }

        var selected = existing[0];
        return new ReferencedFileResolution(
            selected.Path,
            selected.Candidate.SourceKind,
            ClassifyMessageSection(message, selected.Candidate.MessageIndex),
            SelectionIndex: 1,
            AvailableFileCount: existing.Count);
    }

    public static string? ResolveExistingFile(string? reference, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var resolved = TryResolve(reference, workingDirectory);
        return resolved is not null && System.IO.File.Exists(resolved) ? resolved : null;
    }

    private static void AddMatches(
        System.Collections.Generic.ICollection<FileReferenceCandidate> candidates,
        System.Text.RegularExpressions.Regex pattern,
        ReferencedFileSourceKind sourceKind,
        string message)
    {
        foreach (System.Text.RegularExpressions.Match match in pattern.Matches(message))
        {
            if (match.Groups["value"].Success)
            {
                candidates.Add(new FileReferenceCandidate(
                    match.Groups["value"].Value,
                    sourceKind,
                    match.Groups["value"].Index));
            }
        }
    }

    private static ReferencedFileMessageSection ClassifyMessageSection(string message, int messageIndex)
    {
        var approvalStart = message.IndexOf(ApprovalStartMarker, System.StringComparison.Ordinal);
        if (approvalStart >= 0 && messageIndex >= approvalStart)
        {
            return ReferencedFileMessageSection.ApprovalPlannedAction;
        }

        var transcriptStart = FirstMarkerIndex(
            message,
            TranscriptDeltaStartMarker,
            TranscriptStartMarker);
        if (transcriptStart >= 0)
        {
            var transcriptEnd = FirstMarkerIndex(
                message,
                transcriptStart,
                TranscriptDeltaEndMarker,
                TranscriptEndMarker);
            if (messageIndex >= transcriptStart &&
                (transcriptEnd < 0 || messageIndex < transcriptEnd))
            {
                return ReferencedFileMessageSection.ApprovalTranscript;
            }
        }

        return approvalStart >= 0
            ? ReferencedFileMessageSection.ApprovalContext
            : ReferencedFileMessageSection.MessageBody;
    }

    private static int FirstMarkerIndex(string message, params string[] markers) =>
        FirstMarkerIndex(message, 0, markers);

    private static int FirstMarkerIndex(string message, int startIndex, params string[] markers)
    {
        var first = -1;
        foreach (var marker in markers)
        {
            var index = message.IndexOf(marker, startIndex, System.StringComparison.Ordinal);
            if (index >= 0 && (first < 0 || index < first))
            {
                first = index;
            }
        }

        return first;
    }

    private static string? TryResolve(string value, string? workingDirectory)
    {
        var candidate = value.Trim().Trim('<', '>', '"', '\'');
        if (candidate.StartsWith("file://", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!System.Uri.TryCreate(candidate, System.UriKind.Absolute, out var fileUri) || !fileUri.IsFile)
            {
                return null;
            }

            candidate = fileUri.LocalPath;
        }
        else if (System.Uri.TryCreate(candidate, System.UriKind.Absolute, out var uri) &&
                 !string.Equals(uri.Scheme, System.Uri.UriSchemeFile, System.StringComparison.OrdinalIgnoreCase) &&
                 !IsWindowsDrivePath(candidate))
        {
            return null;
        }

        candidate = candidate
            .Replace("%20", " ", System.StringComparison.OrdinalIgnoreCase)
            .TrimEnd(' ', '.', ',', ';', '!', '?');
        candidate = LineSuffixPattern.Replace(candidate, string.Empty);
        candidate = candidate.TrimEnd(' ', '.', ',', ';', '!', '?');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            candidate = System.Environment.ExpandEnvironmentVariables(candidate);
            if (System.IO.Path.IsPathFullyQualified(candidate))
            {
                return System.IO.Path.GetFullPath(candidate);
            }

            if (string.IsNullOrWhiteSpace(workingDirectory) ||
                (!candidate.Contains(System.IO.Path.DirectorySeparatorChar) &&
                 !candidate.Contains(System.IO.Path.AltDirectorySeparatorChar) &&
                 string.IsNullOrWhiteSpace(System.IO.Path.GetExtension(candidate))))
            {
                return null;
            }

            return System.IO.Path.GetFullPath(candidate, workingDirectory);
        }
        catch (System.Exception ex) when (
            ex is System.ArgumentException or
                System.NotSupportedException or
                System.IO.PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsWindowsDrivePath(string value) =>
        value.Length >= 3 &&
        char.IsAsciiLetter(value[0]) &&
        value[1] == ':' &&
        value[2] is '\\' or '/';

    private sealed record FileReferenceCandidate(
        string Value,
        ReferencedFileSourceKind SourceKind,
        int MessageIndex);

    private sealed record ResolvedCandidate(FileReferenceCandidate Candidate, string Path);
}
