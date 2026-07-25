namespace VolturaAiWatcher;

public static class ReferencedFileResolver
{
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

    public static string? ResolveFirstExistingFile(string? message, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var candidates = new System.Collections.Generic.List<string>();
        AddMatches(candidates, MarkdownTargetPattern, message);
        AddMatches(candidates, InlineCodePattern, message);
        AddMatches(candidates, AbsoluteWindowsPathPattern, message);

        foreach (var candidate in candidates.Distinct(System.StringComparer.OrdinalIgnoreCase))
        {
            var resolved = TryResolve(candidate, workingDirectory);
            if (resolved is not null && System.IO.File.Exists(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static void AddMatches(
        System.Collections.Generic.ICollection<string> candidates,
        System.Text.RegularExpressions.Regex pattern,
        string message)
    {
        foreach (System.Text.RegularExpressions.Match match in pattern.Matches(message))
        {
            if (match.Groups["value"].Success)
            {
                candidates.Add(match.Groups["value"].Value);
            }
        }
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
}

