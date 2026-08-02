namespace VolturaAiWatcher;

public sealed record CodexObservedMessage(
    string Id,
    string ThreadId,
    string ProjectName,
    CodexProjectMetadata ProjectMetadata,
    string? WorkingDirectory,
    string ChatTitle,
    string Sender,
    string Text,
    System.DateTimeOffset OccurredAt,
    CodexChatStatus Status);

public sealed record CodexObservedUsage(
    string ThreadId,
    CodexUsageSnapshot Usage,
    bool Historical);

public sealed class CodexSessionMonitor : System.IDisposable
{
    private const int HistoricalFileLimit = 50;
    private const int HistoricalTailBytes = 2 * 1024 * 1024;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, FileCursor> _cursors =
        new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _titles =
        new(System.StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CodexProjectMetadata> _projectMetadataByThread =
        new(System.StringComparer.Ordinal);
    private readonly System.Threading.Channels.Channel<MonitorWork> _work =
        System.Threading.Channels.Channel.CreateUnbounded<MonitorWork>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    private readonly System.Threading.CancellationTokenSource _cancellation = new();
    private readonly System.Threading.Tasks.Task _worker;
    private System.IO.FileSystemWatcher? _sessionsWatcher;
    private System.IO.FileSystemWatcher? _metadataWatcher;
    private bool _started;
    private bool _disposed;

    public CodexSessionMonitor(string? codexHome = null)
    {
        CodexHome = string.IsNullOrWhiteSpace(codexHome)
            ? ResolveCodexHome()
            : System.IO.Path.GetFullPath(codexHome);
        SessionsPath = System.IO.Path.Combine(CodexHome, "sessions");
        SessionIndexPath = System.IO.Path.Combine(CodexHome, "session_index.jsonl");
        GlobalStatePath = System.IO.Path.Combine(CodexHome, ".codex-global-state.json");
        _worker = System.Threading.Tasks.Task.Run(ProcessWorkAsync);
    }

    public string CodexHome { get; }
    public string SessionsPath { get; }
    public string SessionIndexPath { get; }
    public string GlobalStatePath { get; }

    public event System.Action<CodexObservedMessage, bool>? MessageObserved;
    public event System.Action<string, CodexChatStatus, System.DateTimeOffset, bool>? StatusObserved;
    public event System.Action<CodexObservedUsage>? UsageObserved;
    public event System.Action<string, string>? TitleObserved;
    public event System.Action<System.Collections.Generic.IReadOnlySet<string>>? UnreadThreadsChanged;
    public event System.Action<string>? MonitorWarning;
    public event System.Action? ProjectMetadataChanged;

    public async System.Threading.Tasks.Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        LoadTitles();
        LoadUnreadThreads();
        LoadProjectMetadata();

        if (!System.IO.Directory.Exists(SessionsPath))
        {
            MonitorWarning?.Invoke($"Codex sessions folder was not found: {SessionsPath}");
            return;
        }

        _sessionsWatcher = new System.IO.FileSystemWatcher(SessionsPath, "rollout-*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = System.IO.NotifyFilters.FileName |
                           System.IO.NotifyFilters.LastWrite |
                           System.IO.NotifyFilters.Size,
            InternalBufferSize = 64 * 1024
        };
        _sessionsWatcher.Changed += SessionFileChanged;
        _sessionsWatcher.Created += SessionFileChanged;
        _sessionsWatcher.Renamed += (_, e) => QueueSessionFile(e.FullPath, historical: false);
        _sessionsWatcher.Error += (_, _) => _work.Writer.TryWrite(new MonitorWork(WorkKind.Reconcile, string.Empty, false));
        _sessionsWatcher.EnableRaisingEvents = true;

        _metadataWatcher = new System.IO.FileSystemWatcher(CodexHome)
        {
            IncludeSubdirectories = false,
            Filter = "*",
            NotifyFilter = System.IO.NotifyFilters.FileName |
                           System.IO.NotifyFilters.LastWrite |
                           System.IO.NotifyFilters.Size
        };
        _metadataWatcher.Changed += MetadataChanged;
        _metadataWatcher.Created += MetadataChanged;
        _metadataWatcher.Renamed += (_, e) => MetadataChanged(this, e);
        _metadataWatcher.EnableRaisingEvents = true;

        var files = await System.Threading.Tasks.Task.Run(() =>
            System.IO.Directory.EnumerateFiles(
                    SessionsPath,
                    "rollout-*.jsonl",
                    System.IO.SearchOption.AllDirectories)
                .Select(path => new System.IO.FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray());

        await System.Threading.Tasks.Task.Run(() =>
        {
            for (var index = 0; index < files.Length; index++)
            {
                InitializeFile(files[index].FullName, includeHistory: index < HistoricalFileLimit);
            }
        });
    }

    public string GetTitle(string threadId) =>
        _titles.TryGetValue(threadId, out var title) && !string.IsNullOrWhiteSpace(title)
            ? title
            : "Untitled chat";

    private static string ResolveCodexHome()
    {
        var configured = System.Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return System.IO.Path.GetFullPath(
                System.Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"')));
        }

        return System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            ".codex");
    }

    private void SessionFileChanged(object sender, System.IO.FileSystemEventArgs e) =>
        QueueSessionFile(e.FullPath, historical: false);

    private void MetadataChanged(object? sender, System.IO.FileSystemEventArgs e)
    {
        if (string.Equals(e.Name, System.IO.Path.GetFileName(SessionIndexPath), System.StringComparison.OrdinalIgnoreCase))
        {
            _work.Writer.TryWrite(new MonitorWork(WorkKind.ReloadTitles, e.FullPath, false));
        }
        else if (string.Equals(e.Name, System.IO.Path.GetFileName(GlobalStatePath), System.StringComparison.OrdinalIgnoreCase))
        {
            _work.Writer.TryWrite(new MonitorWork(WorkKind.ReloadUnread, e.FullPath, false));
        }
    }

    private void QueueSessionFile(string path, bool historical)
    {
        if (!_disposed)
        {
            _work.Writer.TryWrite(new MonitorWork(WorkKind.ReadSession, path, historical));
        }
    }

    private async System.Threading.Tasks.Task ProcessWorkAsync()
    {
        try
        {
            await foreach (var work in _work.Reader.ReadAllAsync(_cancellation.Token))
            {
                try
                {
                    switch (work.Kind)
                    {
                        case WorkKind.ReadSession:
                            ReadAppendedFile(work.Path, work.Historical);
                            break;
                        case WorkKind.ReloadTitles:
                            await DelayForAtomicReplaceAsync();
                            LoadTitles();
                            break;
                        case WorkKind.ReloadUnread:
                            await DelayForAtomicReplaceAsync();
                            LoadUnreadThreads();
                            LoadProjectMetadata();
                            break;
                        case WorkKind.Reconcile:
                            ReconcileSessionFiles();
                            break;
                    }
                }
                catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
                {
                    MonitorWarning?.Invoke($"{System.IO.Path.GetFileName(work.Path)}: {ex.Message}");
                }
            }
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    private async System.Threading.Tasks.Task DelayForAtomicReplaceAsync()
    {
        await System.Threading.Tasks.Task.Delay(80, _cancellation.Token);
    }

    private void InitializeFile(string path, bool includeHistory)
    {
        try
        {
            var cursor = _cursors.GetOrAdd(path, _ => new FileCursor());
            lock (cursor.Sync)
            {
                ReadSessionMetadata(path, cursor);
                if (includeHistory)
                {
                    ReadHistoricalTail(path, cursor);
                }

                cursor.Offset = new System.IO.FileInfo(path).Length;
                cursor.PendingBytes = [];
            }
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            MonitorWarning?.Invoke($"{System.IO.Path.GetFileName(path)}: {ex.Message}");
        }
    }

    private void ReadSessionMetadata(string path, FileCursor cursor)
    {
        using var stream = new System.IO.FileStream(
            path,
            System.IO.FileMode.Open,
            System.IO.FileAccess.Read,
            System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
        using var reader = new System.IO.StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        var line = reader.ReadLine();
        if (line is not null &&
            CodexRecordParser.TryParse(line, out var parsed) &&
            parsed is not null)
        {
            ApplyMetadata(cursor, path, parsed);
        }

        cursor.ThreadId ??= TryReadThreadIdFromFileName(path);
    }

    private void ReadHistoricalTail(string path, FileCursor cursor)
    {
        using var stream = new System.IO.FileStream(
            path,
            System.IO.FileMode.Open,
            System.IO.FileAccess.Read,
            System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
        var start = System.Math.Max(0, stream.Length - HistoricalTailBytes);
        stream.Position = start;
        using var reader = new System.IO.StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: start == 0,
            bufferSize: 16 * 1024,
            leaveOpen: false);
        if (start > 0)
        {
            _ = reader.ReadLine();
        }

        while (reader.ReadLine() is { } line)
        {
            ProcessLine(path, cursor, line, historical: true);
        }
    }

    private void ReadAppendedFile(string path, bool historical)
    {
        if (!System.IO.File.Exists(path))
        {
            return;
        }

        var cursor = _cursors.GetOrAdd(path, _ => new FileCursor());
        lock (cursor.Sync)
        {
            var fileLength = new System.IO.FileInfo(path).Length;
            if (cursor.Offset == 0 && cursor.ThreadId is null)
            {
                ReadSessionMetadata(path, cursor);
            }

            if (fileLength < cursor.Offset)
            {
                cursor.Offset = 0;
                cursor.PendingBytes = [];
                cursor.ThreadId = null;
                cursor.WorkingDirectory = null;
                cursor.Model = null;
                cursor.Usage = null;
            }

            if (fileLength <= cursor.Offset)
            {
                return;
            }

            using var stream = new System.IO.FileStream(
                path,
                System.IO.FileMode.Open,
                System.IO.FileAccess.Read,
                System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
            stream.Position = cursor.Offset;
            var appendedLength = checked((int)System.Math.Min(int.MaxValue, stream.Length - cursor.Offset));
            var appended = new byte[appendedLength];
            var totalRead = 0;
            while (totalRead < appended.Length)
            {
                var read = stream.Read(appended, totalRead, appended.Length - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            cursor.Offset += totalRead;
            if (totalRead != appended.Length)
            {
                System.Array.Resize(ref appended, totalRead);
            }

            var combined = new byte[cursor.PendingBytes.Length + appended.Length];
            cursor.PendingBytes.CopyTo(combined, 0);
            appended.CopyTo(combined, cursor.PendingBytes.Length);

            var lineStart = 0;
            for (var index = 0; index < combined.Length; index++)
            {
                if (combined[index] != (byte)'\n')
                {
                    continue;
                }

                var count = index - lineStart;
                if (count > 0 && combined[index - 1] == (byte)'\r')
                {
                    count--;
                }

                var line = System.Text.Encoding.UTF8.GetString(combined, lineStart, count);
                ProcessLine(path, cursor, line, historical);
                lineStart = index + 1;
            }

            cursor.PendingBytes = lineStart < combined.Length
                ? combined[lineStart..]
                : [];
        }
    }

    private void ProcessLine(string path, FileCursor cursor, string line, bool historical)
    {
        if (!CodexRecordParser.TryParse(line, out var parsed) || parsed is null)
        {
            return;
        }

        ApplyMetadata(cursor, path, parsed);
        var threadId = cursor.ThreadId ?? TryReadThreadIdFromFileName(path);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return;
        }

        if (parsed.Model is { Length: > 0 } model)
        {
            cursor.Model = model;
            if (cursor.Usage is { } existingUsage)
            {
                cursor.Usage = existingUsage with
                {
                    Model = model,
                    ObservedAt = parsed.OccurredAt
                };
                UsageObserved?.Invoke(new CodexObservedUsage(threadId, cursor.Usage, historical));
            }
        }

        if (parsed.Usage is { } usage)
        {
            cursor.Usage = usage with { Model = usage.Model ?? cursor.Model };
            UsageObserved?.Invoke(new CodexObservedUsage(threadId, cursor.Usage, historical));
        }

        if (parsed.Status is { } status)
        {
            cursor.Status = status;
            StatusObserved?.Invoke(threadId, status, parsed.OccurredAt, historical);
        }

        if (parsed.Sender is null || parsed.Message is null)
        {
            return;
        }

        var projectMetadata = GetProjectMetadata(threadId, cursor.WorkingDirectory);
        var effectiveStatus = parsed.Sender == "You"
            ? CodexChatStatus.Starting
            : cursor.Status is CodexChatStatus.Idle
                ? CodexChatStatus.Working
                : cursor.Status;
        var message = new CodexObservedMessage(
            CodexRecordParser.CreateMessageId(threadId, parsed.OccurredAt, parsed.Sender, parsed.Message),
            threadId,
            projectMetadata.Name,
            projectMetadata,
            cursor.WorkingDirectory,
            GetTitle(threadId),
            parsed.Sender,
            parsed.Message,
            parsed.OccurredAt,
            effectiveStatus);
        MessageObserved?.Invoke(message, historical);
    }

    private void ApplyMetadata(FileCursor cursor, string path, CodexParsedRecord parsed)
    {
        if (!string.IsNullOrWhiteSpace(parsed.ThreadId))
        {
            cursor.ThreadId = parsed.ThreadId;
        }

        if (!string.IsNullOrWhiteSpace(parsed.WorkingDirectory))
        {
            cursor.WorkingDirectory = parsed.WorkingDirectory;
        }

        cursor.ThreadId ??= TryReadThreadIdFromFileName(path);
    }

    private void LoadTitles()
    {
        if (!System.IO.File.Exists(SessionIndexPath))
        {
            return;
        }

        var latest = new System.Collections.Generic.Dictionary<string, (string Title, System.DateTimeOffset Updated)>(
            System.StringComparer.Ordinal);
        using var stream = new System.IO.FileStream(
            SessionIndexPath,
            System.IO.FileMode.Open,
            System.IO.FileAccess.Read,
            System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(line);
                var root = document.RootElement;
                var id = root.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
                var title = root.TryGetProperty("thread_name", out var titleProperty) ? titleProperty.GetString() : null;
                var updatedRaw = root.TryGetProperty("updated_at", out var updatedProperty)
                    ? updatedProperty.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                _ = System.DateTimeOffset.TryParse(updatedRaw, out var updated);
                if (!latest.TryGetValue(id, out var existing) || updated >= existing.Updated)
                {
                    latest[id] = (title.Trim(), updated);
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        foreach (var pair in latest)
        {
            _titles[pair.Key] = pair.Value.Title;
            TitleObserved?.Invoke(pair.Key, pair.Value.Title);
        }
    }

    private void LoadUnreadThreads()
    {
        var unread = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        if (System.IO.File.Exists(GlobalStatePath))
        {
            try
            {
                using var stream = new System.IO.FileStream(
                    GlobalStatePath,
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
                using var document = System.Text.Json.JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty(
                        "unread-thread-ids-by-host-v1",
                        out var hosts) &&
                    hosts.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var host in hosts.EnumerateObject())
                    {
                        if (host.Value.ValueKind != System.Text.Json.JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var value in host.Value.EnumerateArray())
                        {
                            if (value.ValueKind == System.Text.Json.JsonValueKind.String &&
                                value.GetString() is { Length: > 0 } id)
                            {
                                unread.Add(id);
                            }
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        UnreadThreadsChanged?.Invoke(unread);
    }

    private void ReconcileSessionFiles()
    {
        if (!System.IO.Directory.Exists(SessionsPath))
        {
            return;
        }

        foreach (var path in System.IO.Directory.EnumerateFiles(
                     SessionsPath,
                     "rollout-*.jsonl",
                     System.IO.SearchOption.AllDirectories))
        {
            QueueSessionFile(path, historical: false);
        }
    }

    private static string? TryReadThreadIdFromFileName(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (name.Length < 36)
        {
            return null;
        }

        var candidate = name[^36..];
        return System.Guid.TryParse(candidate, out _) ? candidate : null;
    }

    public CodexProjectMetadata GetProjectMetadata(string threadId, string? workingDirectory)
    {
        return _projectMetadataByThread.TryGetValue(threadId, out var metadata)
            ? metadata
            : new CodexProjectMetadata(GetProjectName(workingDirectory), "green", null);
    }

    private void LoadProjectMetadata()
    {
        var metadata = new System.Collections.Generic.Dictionary<string, CodexProjectMetadata>(System.StringComparer.Ordinal);
        if (!System.IO.File.Exists(GlobalStatePath))
        {
            return;
        }

        try
        {
            using var stream = new System.IO.FileStream(GlobalStatePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
            using var document = System.Text.Json.JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!TryReadObject(root, "thread-project-assignments", out var assignments) || !TryReadObject(root, "local-projects", out var projects))
            {
                return;
            }

            var hasAppearances = TryReadObject(root, "project-appearances", out var appearances);
            foreach (var assignment in assignments.EnumerateObject())
            {
                if (!TryReadString(assignment.Value, "projectId", out var projectId) ||
                    !projects.TryGetProperty(projectId, out var project) ||
                    !TryReadString(project, "name", out var name))
                {
                    continue;
                }

                var color = "green";
                string? icon = null;
                if (hasAppearances && appearances.TryGetProperty(projectId, out var appearance))
                {
                    _ = TryReadString(appearance, "color", out color);
                    if (TryReadObject(appearance, "marker", out var marker))
                    {
                        _ = TryReadString(marker, "icon", out icon);
                    }
                }

                metadata[assignment.Name] = new CodexProjectMetadata(name, color, icon);
            }
        }
        catch (System.IO.IOException)
        {
            return;
        }
        catch (System.Text.Json.JsonException)
        {
            return;
        }

        _projectMetadataByThread.Clear();
        foreach (var item in metadata)
        {
            _projectMetadataByThread[item.Key] = item.Value;
        }

        ProjectMetadataChanged?.Invoke();
    }

    private static bool TryReadObject(System.Text.Json.JsonElement element, string name, out System.Text.Json.JsonElement value) =>
        element.TryGetProperty(name, out value) && value.ValueKind == System.Text.Json.JsonValueKind.Object;

    private static bool TryReadString(System.Text.Json.JsonElement element, string name, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(name, out var property) && property.ValueKind == System.Text.Json.JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }

    private static string GetProjectName(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return "Codex";
        }

        var trimmed = workingDirectory.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);
        var name = System.IO.Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_sessionsWatcher is not null)
        {
            _sessionsWatcher.EnableRaisingEvents = false;
            _sessionsWatcher.Dispose();
        }

        if (_metadataWatcher is not null)
        {
            _metadataWatcher.EnableRaisingEvents = false;
            _metadataWatcher.Dispose();
        }

        _work.Writer.TryComplete();
        _cancellation.Cancel();
        try
        {
            _worker.Wait(System.TimeSpan.FromSeconds(2));
        }
        catch (System.AggregateException)
        {
        }

        _cancellation.Dispose();
    }

    private enum WorkKind
    {
        ReadSession,
        ReloadTitles,
        ReloadUnread,
        Reconcile
    }

    private sealed record MonitorWork(WorkKind Kind, string Path, bool Historical);

    private sealed class FileCursor
    {
        public object Sync { get; } = new();
        public long Offset { get; set; }
        public byte[] PendingBytes { get; set; } = [];
        public string? ThreadId { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? Model { get; set; }
        public CodexUsageSnapshot? Usage { get; set; }
        public CodexChatStatus Status { get; set; } = CodexChatStatus.Idle;
    }
}
