namespace VolturaAiWatcher;

public sealed record GitRepositorySnapshot(
    string? RepositoryRoot,
    string? Branch,
    string HeadLabel,
    bool IsDetached,
    string? Upstream,
    string? RemoteName,
    string? RemoteMergeReference,
    int ChangedFiles,
    int NewFiles,
    int DeletedFiles,
    long TrackedAddedLines,
    long TrackedRemovedLines,
    long UntrackedAddedLines,
    int UntrackedTextFiles,
    int UntrackedExcludedFiles,
    int Ahead,
    int Behind,
    System.DateTimeOffset ObservedAt,
    string? Error)
{
    public bool IsRepository => !string.IsNullOrWhiteSpace(RepositoryRoot);
    public bool HasChanges => ChangedFiles + NewFiles + DeletedFiles > 0;
    public bool CanCommitAndPush =>
        IsRepository &&
        Error is null &&
        !IsDetached &&
        HasChanges &&
        !string.IsNullOrWhiteSpace(Branch) &&
        !string.IsNullOrWhiteSpace(Upstream) &&
        !string.IsNullOrWhiteSpace(RemoteName) &&
        !string.IsNullOrWhiteSpace(RemoteMergeReference);

    public static GitRepositorySnapshot Unavailable(string message) => new(
        null,
        null,
        "GIT UNAVAILABLE",
        false,
        null,
        null,
        null,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        System.DateTimeOffset.Now,
        message);
}

public sealed record GitCommandResult(
    int ExitCode,
    string Output,
    string Error,
    bool TimedOut = false,
    string? StartError = null)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut && StartError is null;
}

public interface IGitCommandRunner
{
    System.Threading.Tasks.Task<GitCommandResult> RunAsync(
        string workingDirectory,
        System.Collections.Generic.IReadOnlyList<string> arguments,
        System.Threading.CancellationToken cancellationToken = default);
}

public sealed class GitCommandRunner : IGitCommandRunner
{
    private static readonly System.TimeSpan CommandTimeout = System.TimeSpan.FromSeconds(20);

    public async System.Threading.Tasks.Task<GitCommandResult> RunAsync(
        string workingDirectory,
        System.Collections.Generic.IReadOnlyList<string> arguments,
        System.Threading.CancellationToken cancellationToken = default)
    {
        using var timeout = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            if (!process.Start())
            {
                return new GitCommandResult(-1, string.Empty, string.Empty, StartError: "Git could not be started.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                return new GitCommandResult(
                    process.ExitCode,
                    await outputTask,
                    await errorTask);
            }
            catch (System.OperationCanceledException)
            {
                TryKill(process);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return new GitCommandResult(-1, string.Empty, string.Empty, TimedOut: true);
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new GitCommandResult(-1, string.Empty, string.Empty, StartError: ex.Message);
        }
    }

    private static void TryKill(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (System.InvalidOperationException)
        {
        }
    }
}

public sealed record GitOperationResult(bool Succeeded, string Message);

internal sealed record GitStatusParseResult(
    int ChangedFiles,
    int NewFiles,
    int DeletedFiles,
    System.Collections.Generic.IReadOnlyList<string> UntrackedPaths);

public sealed class GitRepositoryService
{
    private const long MaximumEstimatedUntrackedFileBytes = 10 * 1024 * 1024;
    private readonly IGitCommandRunner _runner;

    public GitRepositoryService(IGitCommandRunner? runner = null)
    {
        _runner = runner ?? new GitCommandRunner();
    }

    public async System.Threading.Tasks.Task<GitRepositorySnapshot> GetSnapshotAsync(
        string? workingDirectory,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !System.IO.Directory.Exists(workingDirectory))
        {
            return GitRepositorySnapshot.Unavailable("The Codex working directory is unavailable.");
        }

        var rootResult = await RunAsync(workingDirectory, cancellationToken, "rev-parse", "--show-toplevel");
        if (!rootResult.Succeeded)
        {
            return GitRepositorySnapshot.Unavailable(FormatRepositoryError(rootResult));
        }

        var root = rootResult.Output.Trim();
        if (string.IsNullOrWhiteSpace(root) || !System.IO.Directory.Exists(root))
        {
            return GitRepositorySnapshot.Unavailable("Git returned an unavailable repository root.");
        }

        var branchResult = await RunAsync(root, cancellationToken, "symbolic-ref", "--quiet", "--short", "HEAD");
        var isDetached = !branchResult.Succeeded;
        string? branch = isDetached ? null : branchResult.Output.Trim();
        var headLabel = branch;
        if (isDetached)
        {
            var headResult = await RunAsync(root, cancellationToken, "rev-parse", "--short", "HEAD");
            headLabel = headResult.Succeeded && !string.IsNullOrWhiteSpace(headResult.Output)
                ? $"DETACHED {headResult.Output.Trim()}"
                : "DETACHED HEAD";
        }

        var statusResult = await RunAsync(
            root,
            cancellationToken,
            "status",
            "--porcelain=v1",
            "-z",
            "--untracked-files=all");
        if (!statusResult.Succeeded)
        {
            return CreateErrorSnapshot(root, branch, headLabel ?? "GIT", isDetached, FormatCommandError("status", statusResult));
        }

        var parsedStatus = ParsePorcelainStatus(statusResult.Output);
        var numStatResult = await RunAsync(root, cancellationToken, "diff", "--numstat", "-z", "HEAD", "--");
        if (!numStatResult.Succeeded)
        {
            var headExists = await RunAsync(root, cancellationToken, "rev-parse", "--verify", "HEAD");
            if (!headExists.Succeeded)
            {
                numStatResult = await RunAsync(root, cancellationToken, "diff", "--cached", "--numstat", "-z", "--");
            }
        }

        if (!numStatResult.Succeeded)
        {
            return CreateErrorSnapshot(root, branch, headLabel ?? "GIT", isDetached, FormatCommandError("diff", numStatResult));
        }

        var (trackedAdded, trackedRemoved) = ParseNumStat(numStatResult.Output);
        var (untrackedLines, untrackedTextFiles, untrackedExcludedFiles) =
            EstimateUntrackedLines(root, parsedStatus.UntrackedPaths);

        string? upstream = null;
        string? remoteName = null;
        string? remoteMergeReference = null;
        var ahead = 0;
        var behind = 0;
        if (!isDetached && !string.IsNullOrWhiteSpace(branch))
        {
            var upstreamResult = await RunAsync(
                root,
                cancellationToken,
                "rev-parse",
                "--abbrev-ref",
                "--symbolic-full-name",
                "@{upstream}");
            if (upstreamResult.Succeeded)
            {
                upstream = upstreamResult.Output.Trim();
                var remoteResult = await RunAsync(root, cancellationToken, "config", "--get", $"branch.{branch}.remote");
                var mergeResult = await RunAsync(root, cancellationToken, "config", "--get", $"branch.{branch}.merge");
                remoteName = remoteResult.Succeeded ? remoteResult.Output.Trim() : null;
                remoteMergeReference = mergeResult.Succeeded ? mergeResult.Output.Trim() : null;

                var aheadBehindResult = await RunAsync(
                    root,
                    cancellationToken,
                    "rev-list",
                    "--left-right",
                    "--count",
                    "HEAD...@{upstream}");
                (ahead, behind) = ParseAheadBehind(aheadBehindResult.Output, aheadBehindResult.Succeeded);
            }
        }

        return new GitRepositorySnapshot(
            root,
            branch,
            headLabel ?? "GIT",
            isDetached,
            upstream,
            remoteName,
            remoteMergeReference,
            parsedStatus.ChangedFiles,
            parsedStatus.NewFiles,
            parsedStatus.DeletedFiles,
            trackedAdded,
            trackedRemoved,
            untrackedLines,
            untrackedTextFiles,
            untrackedExcludedFiles,
            ahead,
            behind,
            System.DateTimeOffset.Now,
            null);
    }

    public async System.Threading.Tasks.Task<GitOperationResult> CommitAndPushAsync(
        GitRepositorySnapshot confirmedSnapshot,
        string commitMessage,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var trimmedMessage = commitMessage.Trim();
        if (trimmedMessage.Length == 0)
        {
            return new GitOperationResult(false, "Enter a commit message.");
        }

        if (!confirmedSnapshot.CanCommitAndPush || string.IsNullOrWhiteSpace(confirmedSnapshot.RepositoryRoot))
        {
            return new GitOperationResult(false, "This repository is not ready to commit and push.");
        }

        var current = await GetSnapshotAsync(confirmedSnapshot.RepositoryRoot, cancellationToken);
        if (!current.CanCommitAndPush ||
            !string.Equals(current.RepositoryRoot, confirmedSnapshot.RepositoryRoot, System.StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(current.Branch, confirmedSnapshot.Branch, System.StringComparison.Ordinal) ||
            !string.Equals(current.Upstream, confirmedSnapshot.Upstream, System.StringComparison.Ordinal))
        {
            return new GitOperationResult(false, "The branch, upstream, or repository status changed. Refresh and review it again.");
        }

        var root = current.RepositoryRoot!;
        var addResult = await RunAsync(root, cancellationToken, "add", "--all", "--", ".");
        if (!addResult.Succeeded)
        {
            return new GitOperationResult(false, FormatCommandError("stage changes", addResult));
        }

        var stagedResult = await RunAsync(root, cancellationToken, "diff", "--cached", "--quiet");
        if (stagedResult.ExitCode == 0)
        {
            return new GitOperationResult(false, "No staged changes remain to commit.");
        }

        if (stagedResult.ExitCode != 1)
        {
            return new GitOperationResult(false, FormatCommandError("verify staged changes", stagedResult));
        }

        var commitResult = await RunAsync(root, cancellationToken, "commit", "-m", trimmedMessage);
        if (!commitResult.Succeeded)
        {
            return new GitOperationResult(false, FormatCommandError("commit", commitResult));
        }

        var pushResult = await RunAsync(
            root,
            cancellationToken,
            "push",
            current.RemoteName!,
            $"HEAD:{current.RemoteMergeReference}");
        if (!pushResult.Succeeded)
        {
            return new GitOperationResult(
                false,
                $"The commit was created locally, but push failed. {FormatCommandError("push", pushResult)}");
        }

        return new GitOperationResult(true, $"Committed and pushed {current.Branch} to {current.Upstream}.");
    }

    internal static GitStatusParseResult ParsePorcelainStatus(string output)
    {
        var changed = 0;
        var added = 0;
        var deleted = 0;
        var untracked = new System.Collections.Generic.List<string>();
        var records = output.Split('\0', System.StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Length < 3)
            {
                continue;
            }

            var x = record[0];
            var y = record[1];
            var path = record[3..];
            if (x == '?' && y == '?')
            {
                added++;
                untracked.Add(path);
                continue;
            }

            if (x == 'D' || y == 'D')
            {
                deleted++;
            }
            else if (x == 'A' || y == 'A')
            {
                added++;
            }
            else
            {
                changed++;
            }

            if ((x == 'R' || x == 'C') && index + 1 < records.Length)
            {
                index++;
            }
        }

        return new GitStatusParseResult(changed, added, deleted, untracked);
    }

    internal static (long Added, long Removed) ParseNumStat(string output)
    {
        long added = 0;
        long removed = 0;
        var records = output.Split('\0');
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (string.IsNullOrEmpty(record))
            {
                continue;
            }

            var firstTab = record.IndexOf('\t');
            var secondTab = firstTab < 0 ? -1 : record.IndexOf('\t', firstTab + 1);
            if (firstTab < 0 || secondTab < 0)
            {
                continue;
            }

            if (long.TryParse(record[..firstTab], out var fileAdded))
            {
                added += fileAdded;
            }

            if (long.TryParse(record[(firstTab + 1)..secondTab], out var fileRemoved))
            {
                removed += fileRemoved;
            }

            if (secondTab == record.Length - 1)
            {
                index += 2;
            }
        }

        return (added, removed);
    }

    internal static (int Ahead, int Behind) ParseAheadBehind(string output, bool succeeded)
    {
        if (!succeeded)
        {
            return (0, 0);
        }

        var parts = output.Trim().Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[0], out var ahead) && int.TryParse(parts[1], out var behind)
            ? (ahead, behind)
            : (0, 0);
    }

    private static (long Lines, int TextFiles, int ExcludedFiles) EstimateUntrackedLines(
        string root,
        System.Collections.Generic.IReadOnlyList<string> paths)
    {
        long lines = 0;
        var textFiles = 0;
        var excludedFiles = 0;
        var rootPrefix = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root)) +
                         System.IO.Path.DirectorySeparatorChar;
        foreach (var relativePath in paths)
        {
            try
            {
                var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relativePath));
                if (!fullPath.StartsWith(rootPrefix, System.StringComparison.OrdinalIgnoreCase) ||
                    !System.IO.File.Exists(fullPath) ||
                    new System.IO.FileInfo(fullPath).Length > MaximumEstimatedUntrackedFileBytes ||
                    !TryCountTextLines(fullPath, out var fileLines))
                {
                    excludedFiles++;
                    continue;
                }

                lines += fileLines;
                textFiles++;
            }
            catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException or System.ArgumentException)
            {
                excludedFiles++;
            }
        }

        return (lines, textFiles, excludedFiles);
    }

    private static bool TryCountTextLines(string path, out long lines)
    {
        lines = 0;
        using var stream = new System.IO.FileStream(
            path,
            System.IO.FileMode.Open,
            System.IO.FileAccess.Read,
            System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
        var buffer = new byte[64 * 1024];
        var hasBytes = false;
        byte lastByte = 0;
        while (stream.Read(buffer, 0, buffer.Length) is var read && read > 0)
        {
            hasBytes = true;
            for (var index = 0; index < read; index++)
            {
                if (buffer[index] == 0)
                {
                    lines = 0;
                    return false;
                }

                if (buffer[index] == (byte)'\n')
                {
                    lines++;
                }
            }

            lastByte = buffer[read - 1];
        }

        if (hasBytes && lastByte != (byte)'\n')
        {
            lines++;
        }

        return true;
    }

    private async System.Threading.Tasks.Task<GitCommandResult> RunAsync(
        string workingDirectory,
        System.Threading.CancellationToken cancellationToken,
        params string[] arguments) =>
        await _runner.RunAsync(workingDirectory, arguments, cancellationToken);

    private static GitRepositorySnapshot CreateErrorSnapshot(
        string root,
        string? branch,
        string headLabel,
        bool isDetached,
        string error) => new(
            root,
            branch,
            headLabel,
            isDetached,
            null,
            null,
            null,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            System.DateTimeOffset.Now,
            error);

    private static string FormatRepositoryError(GitCommandResult result)
    {
        if (result.StartError is not null)
        {
            return "Git is not installed or could not be started.";
        }

        if (result.TimedOut)
        {
            return "Git did not respond while locating the repository.";
        }

        return "The Codex working directory is not inside a Git repository.";
    }

    private static string FormatCommandError(string operation, GitCommandResult result)
    {
        if (result.StartError is not null)
        {
            return $"Git could not start while trying to {operation}.";
        }

        if (result.TimedOut)
        {
            return $"Git timed out while trying to {operation}.";
        }

        var detail = result.Error.Trim();
        if (detail.Length > 240)
        {
            detail = detail[..240].TrimEnd() + "...";
        }

        return detail.Length == 0
            ? $"Git could not {operation} (exit code {result.ExitCode})."
            : $"Git could not {operation}: {detail}";
    }
}

public static class GitRepositoryFormatter
{
    public static string FormatHeader(GitRepositorySnapshot? snapshot, bool isRefreshing)
    {
        if (isRefreshing)
        {
            return "GIT REFRESHING...";
        }

        if (snapshot is null)
        {
            return "GIT LOADING...";
        }

        if (!snapshot.IsRepository)
        {
            return "GIT UNAVAILABLE";
        }

        return $"{snapshot.HeadLabel.ToUpperInvariant()} // ~{snapshot.ChangedFiles} +{snapshot.NewFiles} -{snapshot.DeletedFiles}";
    }

    public static string FormatToolTip(GitRepositorySnapshot? snapshot, bool isRefreshing)
    {
        if (isRefreshing)
        {
            return "Refreshing repository status...";
        }

        if (snapshot is null)
        {
            return "Repository status has not been loaded yet.";
        }

        if (!snapshot.IsRepository)
        {
            return snapshot.Error ?? "Repository status is unavailable.";
        }

        var lines = new System.Collections.Generic.List<string>
        {
            $"Repository: {snapshot.RepositoryRoot}",
            $"Branch: {snapshot.HeadLabel}",
            $"Upstream: {snapshot.Upstream ?? "not configured"}"
        };
        if (snapshot.Upstream is not null)
        {
            lines.Add($"Remote difference: {snapshot.Ahead} ahead / {snapshot.Behind} behind");
        }

        lines.Add($"Files: {snapshot.ChangedFiles} changed / {snapshot.NewFiles} new / {snapshot.DeletedFiles} deleted");
        lines.Add($"Tracked rows: +{snapshot.TrackedAddedLines} / -{snapshot.TrackedRemovedLines}");
        lines.Add($"Untracked rows (estimated): +{snapshot.UntrackedAddedLines} in {snapshot.UntrackedTextFiles} text files");
        if (snapshot.UntrackedExcludedFiles > 0)
        {
            lines.Add($"Untracked files excluded from row estimate: {snapshot.UntrackedExcludedFiles}");
        }

        if (snapshot.Error is not null)
        {
            lines.Add($"Status: {snapshot.Error}");
        }
        else if (!snapshot.HasChanges)
        {
            lines.Add("Status: working tree clean");
        }
        else if (!snapshot.CanCommitAndPush)
        {
            lines.Add(snapshot.IsDetached
                ? "Commit + push unavailable while HEAD is detached"
                : "Commit + push unavailable until this branch has an upstream");
        }

        lines.Add($"Refreshed: {snapshot.ObservedAt:yyyy-MM-dd HH:mm:ss}");
        return string.Join("\n", lines);
    }
}

public static class GitCommitMessageFormatter
{
    public static string CreateDefault(string projectName, string chatTitle)
    {
        var project = string.IsNullOrWhiteSpace(projectName) ? "repository" : projectName.Trim();
        var title = string.IsNullOrWhiteSpace(chatTitle) ||
                    string.Equals(chatTitle.Trim(), "Untitled chat", System.StringComparison.OrdinalIgnoreCase)
            ? "Codex changes"
            : chatTitle.Trim();
        var message = $"Update {project}: {title}";
        return message.Length <= 72 ? message : message[..72].TrimEnd();
    }
}
