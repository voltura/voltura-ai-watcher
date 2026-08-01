namespace VolturaAiWatcher.Tests;

public sealed class GitRepositoryServiceTests
{
    [Fact]
    public void ParsesChangedNewDeletedRenamedAndConflictedFiles()
    {
        var result = VolturaAiWatcher.GitRepositoryService.ParsePorcelainStatus(
            " M file.cs\0A  added.cs\0 D deleted.cs\0?? new.cs\0R  renamed.cs\0old.cs\0UU conflict.cs\0");

        Assert.Equal(3, result.ChangedFiles);
        Assert.Equal(2, result.NewFiles);
        Assert.Equal(1, result.DeletedFiles);
        Assert.Equal(["new.cs"], result.UntrackedPaths);
    }

    [Fact]
    public void ParsesTrackedNumStatAndIgnoresBinaryCounts()
    {
        var (added, removed) = VolturaAiWatcher.GitRepositoryService.ParseNumStat(
            "10\t2\tfile.cs\0-\t-\tbinary.bin\0");

        Assert.Equal(10, added);
        Assert.Equal(2, removed);
    }

    [Fact]
    public void ParsesAheadAndBehindCounts()
    {
        Assert.Equal(
            (3, 2),
            VolturaAiWatcher.GitRepositoryService.ParseAheadBehind("3\t2\n", succeeded: true));
        Assert.Equal(
            (0, 0),
            VolturaAiWatcher.GitRepositoryService.ParseAheadBehind("invalid", succeeded: true));
    }

    [Fact]
    public void HeaderAlwaysIncludesActiveBranch()
    {
        var snapshot = CreateSnapshot(branch: "feature/git-status");

        var header = VolturaAiWatcher.GitRepositoryFormatter.FormatHeader(snapshot, isRefreshing: false);

        Assert.StartsWith("FEATURE/GIT-STATUS //", header);
        Assert.Contains("~1 +2 -3", header);
    }

    [Fact]
    public void HeaderMakesDetachedHeadExplicit()
    {
        var snapshot = CreateSnapshot(branch: null) with
        {
            HeadLabel = "DETACHED a1b2c3d",
            IsDetached = true,
            Upstream = null,
            RemoteName = null,
            RemoteMergeReference = null
        };

        Assert.StartsWith(
            "DETACHED A1B2C3D //",
            VolturaAiWatcher.GitRepositoryFormatter.FormatHeader(snapshot, isRefreshing: false));
        Assert.False(snapshot.CanCommitAndPush);
    }

    [Fact]
    public void MissingUpstreamDisablesCommitAndPush()
    {
        var snapshot = CreateSnapshot("feature/local") with
        {
            Upstream = null,
            RemoteName = null,
            RemoteMergeReference = null
        };

        Assert.False(snapshot.CanCommitAndPush);
        Assert.Contains(
            "until this branch has an upstream",
            VolturaAiWatcher.GitRepositoryFormatter.FormatToolTip(snapshot, isRefreshing: false));
    }

    [Fact]
    public void UntrackedRowEstimateShowsOnlyAddedRows()
    {
        var tooltip = VolturaAiWatcher.GitRepositoryFormatter.FormatToolTip(
            CreateSnapshot("main"),
            isRefreshing: false);

        Assert.Contains("Untracked rows (estimated): +8 in 2 text files", tooltip);
        Assert.DoesNotContain("Untracked rows (estimated): +8 / -0", tooltip);
    }

    [Fact]
    public void CreatesEditableGeneratedCommitMessageWithinSubjectLimit()
    {
        var message = VolturaAiWatcher.GitCommitMessageFormatter.CreateDefault(
            "voltura-ai-watcher",
            new string('x', 100));

        Assert.StartsWith("Update voltura-ai-watcher:", message);
        Assert.True(message.Length <= 72);
        Assert.Equal(
            "Update watcher: Codex changes",
            VolturaAiWatcher.GitCommitMessageFormatter.CreateDefault("watcher", "Untitled chat"));
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadsBranchUpstreamCountsAndStatistics()
    {
        var root = System.IO.Path.GetFullPath(".");
        var runner = CreateSnapshotRunner(root, " M source.cs\0A  staged.cs\0 D removed.cs\0", "12\t4\tsource.cs\0");
        var service = new VolturaAiWatcher.GitRepositoryService(runner);

        var snapshot = await service.GetSnapshotAsync(root);

        Assert.Equal(root, snapshot.RepositoryRoot);
        Assert.Equal("main", snapshot.Branch);
        Assert.Equal("origin/main", snapshot.Upstream);
        Assert.Equal(1, snapshot.ChangedFiles);
        Assert.Equal(1, snapshot.NewFiles);
        Assert.Equal(1, snapshot.DeletedFiles);
        Assert.Equal(12, snapshot.TrackedAddedLines);
        Assert.Equal(4, snapshot.TrackedRemovedLines);
        Assert.Equal(2, snapshot.Ahead);
        Assert.Equal(1, snapshot.Behind);
        Assert.True(snapshot.CanCommitAndPush);
    }

    [Fact]
    public async System.Threading.Tasks.Task KeepsUntrackedRowEstimateSeparateAndExcludesBinaryFiles()
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"VolturaAiWatcher-GitTests-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "notes.txt"), "one\ntwo\n");
            await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(root, "image.bin"), [1, 0, 2]);
            var runner = CreateSnapshotRunner(root, "?? notes.txt\0?? image.bin\0", string.Empty);
            var service = new VolturaAiWatcher.GitRepositoryService(runner);

            var snapshot = await service.GetSnapshotAsync(root);

            Assert.Equal(2, snapshot.NewFiles);
            Assert.Equal(2, snapshot.UntrackedAddedLines);
            Assert.Equal(1, snapshot.UntrackedTextFiles);
            Assert.Equal(1, snapshot.UntrackedExcludedFiles);
            Assert.Equal(0, snapshot.TrackedAddedLines);
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task ReportsMissingGitWithoutThrowing()
    {
        var runner = new FakeGitCommandRunner((_, _) =>
            new VolturaAiWatcher.GitCommandResult(-1, string.Empty, string.Empty, StartError: "missing"));
        var service = new VolturaAiWatcher.GitRepositoryService(runner);

        var snapshot = await service.GetSnapshotAsync(System.IO.Path.GetFullPath("."));

        Assert.False(snapshot.IsRepository);
        Assert.Contains("not installed", snapshot.Error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async System.Threading.Tasks.Task ReportsGitTimeoutWithoutThrowing()
    {
        var runner = new FakeGitCommandRunner((_, _) =>
            new VolturaAiWatcher.GitCommandResult(-1, string.Empty, string.Empty, TimedOut: true));
        var service = new VolturaAiWatcher.GitRepositoryService(runner);

        var snapshot = await service.GetSnapshotAsync(System.IO.Path.GetFullPath("."));

        Assert.False(snapshot.IsRepository);
        Assert.Contains("did not respond", snapshot.Error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async System.Threading.Tasks.Task CommitAndPushStagesEverythingAndTargetsConfiguredUpstream()
    {
        var root = System.IO.Path.GetFullPath(".");
        var runner = CreateSnapshotRunner(root, " M source.cs\0", "1\t0\tsource.cs\0");
        var service = new VolturaAiWatcher.GitRepositoryService(runner);
        var confirmed = CreateSnapshot("main") with { RepositoryRoot = root };

        var result = await service.CommitAndPushAsync(confirmed, "Update watcher Git status");

        Assert.True(result.Succeeded);
        Assert.Contains(runner.Calls, call => call.SequenceEqual(["add", "--all", "--", "."]));
        Assert.Contains(runner.Calls, call => call.SequenceEqual(["commit", "-m", "Update watcher Git status"]));
        Assert.Contains(runner.Calls, call => call.SequenceEqual(["push", "origin", "HEAD:refs/heads/main"]));
    }

    [Fact]
    public async System.Threading.Tasks.Task BlankCommitMessageRunsNoGitCommands()
    {
        var runner = new FakeGitCommandRunner((_, _) =>
            new VolturaAiWatcher.GitCommandResult(0, string.Empty, string.Empty));
        var service = new VolturaAiWatcher.GitRepositoryService(runner);

        var result = await service.CommitAndPushAsync(CreateSnapshot("main"), "  ");

        Assert.False(result.Succeeded);
        Assert.Empty(runner.Calls);
    }

    private static VolturaAiWatcher.GitRepositorySnapshot CreateSnapshot(string? branch) => new(
        System.IO.Path.GetFullPath("."),
        branch,
        branch ?? "DETACHED a1b2c3d",
        branch is null,
        branch is null ? null : "origin/main",
        branch is null ? null : "origin",
        branch is null ? null : "refs/heads/main",
        1,
        2,
        3,
        12,
        4,
        8,
        2,
        1,
        2,
        1,
        System.DateTimeOffset.Parse("2026-08-01T12:00:00+02:00"),
        null);

    private static FakeGitCommandRunner CreateSnapshotRunner(string root, string status, string numStat) =>
        new((_, arguments) =>
        {
            var command = string.Join("\u001f", arguments);
            return command switch
            {
                "rev-parse\u001f--show-toplevel" => Success(root + System.Environment.NewLine),
                "symbolic-ref\u001f--quiet\u001f--short\u001fHEAD" => Success("main\n"),
                "status\u001f--porcelain=v1\u001f-z\u001f--untracked-files=all" => Success(status),
                "diff\u001f--numstat\u001f-z\u001fHEAD\u001f--" => Success(numStat),
                "rev-parse\u001f--abbrev-ref\u001f--symbolic-full-name\u001f@{upstream}" => Success("origin/main\n"),
                "config\u001f--get\u001fbranch.main.remote" => Success("origin\n"),
                "config\u001f--get\u001fbranch.main.merge" => Success("refs/heads/main\n"),
                "rev-list\u001f--left-right\u001f--count\u001fHEAD...@{upstream}" => Success("2\t1\n"),
                "add\u001f--all\u001f--\u001f." => Success(),
                "diff\u001f--cached\u001f--quiet" => new VolturaAiWatcher.GitCommandResult(1, string.Empty, string.Empty),
                "commit\u001f-m\u001fUpdate watcher Git status" => Success("committed"),
                "push\u001forigin\u001fHEAD:refs/heads/main" => Success("pushed"),
                _ => new VolturaAiWatcher.GitCommandResult(2, string.Empty, $"Unexpected command: {command}")
            };
        });

    private static VolturaAiWatcher.GitCommandResult Success(string output = "") =>
        new(0, output, string.Empty);

    private sealed class FakeGitCommandRunner : VolturaAiWatcher.IGitCommandRunner
    {
        private readonly System.Func<string, System.Collections.Generic.IReadOnlyList<string>, VolturaAiWatcher.GitCommandResult> _handler;

        public FakeGitCommandRunner(
            System.Func<string, System.Collections.Generic.IReadOnlyList<string>, VolturaAiWatcher.GitCommandResult> handler)
        {
            _handler = handler;
        }

        public System.Collections.Generic.List<string[]> Calls { get; } = [];

        public System.Threading.Tasks.Task<VolturaAiWatcher.GitCommandResult> RunAsync(
            string workingDirectory,
            System.Collections.Generic.IReadOnlyList<string> arguments,
            System.Threading.CancellationToken cancellationToken = default)
        {
            Calls.Add(arguments.ToArray());
            return System.Threading.Tasks.Task.FromResult(_handler(workingDirectory, arguments));
        }
    }
}
