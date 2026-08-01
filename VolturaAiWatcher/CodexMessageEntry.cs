namespace VolturaAiWatcher;

public sealed class CodexMessageEntry : System.ComponentModel.INotifyPropertyChanged
{
    private string _chatTitle = "Untitled chat";
    private CodexChatStatus _status;
    private bool _isUnread;
    private bool _isCleared;
    private bool _isLatestForThread;
    private string _text = string.Empty;
    private StructuredMessagePresentation? _structuredPresentation;
    private CodexUsageSnapshot? _usage;
    private CodexUsageSnapshot? _weeklyUsage;
    private GitRepositorySnapshot? _gitRepository;
    private bool _isGitRefreshing;

    public required string Id { get; init; }
    public required string ThreadId { get; init; }
    public required string ProjectName { get; init; }
    public string? WorkingDirectory { get; init; }
    public required string Sender { get; init; }
    public required string Text
    {
        get => _text;
        init
        {
            _text = value;
            _structuredPresentation = StructuredMessageFormatter.TryFormat(value);
        }
    }
    public required System.DateTimeOffset OccurredAt { get; init; }

    public string ChatTitle
    {
        get => _chatTitle;
        set
        {
            if (SetField(ref _chatTitle, string.IsNullOrWhiteSpace(value) ? "Untitled chat" : value.Trim()))
            {
                OnPropertyChanged(nameof(HeaderText));
            }
        }
    }

    public CodexChatStatus Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusSummary));
                OnPropertyChanged(nameof(IsWorking));
            }
        }
    }

    public bool IsUnread
    {
        get => _isUnread;
        set
        {
            if (SetField(ref _isUnread, value))
            {
                OnPropertyChanged(nameof(StatusSummary));
            }
        }
    }

    public bool IsCleared
    {
        get => _isCleared;
        set => SetField(ref _isCleared, value);
    }

    public bool IsLatestForThread
    {
        get => _isLatestForThread;
        set
        {
            if (SetField(ref _isLatestForThread, value))
            {
                OnPropertyChanged(nameof(UsageToolTip));
            }
        }
    }

    public CodexUsageSnapshot? Usage
    {
        get => _usage;
        set
        {
            if (SetField(ref _usage, value))
            {
                OnPropertyChanged(nameof(UsageToolTip));
                OnPropertyChanged(nameof(DetailUsageText));
                OnPropertyChanged(nameof(DetailUsageToolTip));
            }
        }
    }

    public CodexUsageSnapshot? WeeklyUsage
    {
        get => _weeklyUsage;
        set
        {
            if (SetField(ref _weeklyUsage, value))
            {
                OnPropertyChanged(nameof(WeeklyUsageText));
                OnPropertyChanged(nameof(WeeklyUsageToolTip));
            }
        }
    }

    public string HeaderText => $"{Sender.ToUpperInvariant()} // {ProjectName.ToUpperInvariant()} // {ChatTitle.ToUpperInvariant()}";
    public string PreviewText => _structuredPresentation?.PreviewText ?? Text;
    public string DisplayText => _structuredPresentation?.DetailText ?? Text;
    public string DetailHeading => _structuredPresentation is null ? "COMPLETE CODEX MESSAGE" : "STRUCTURED CODEX MESSAGE";
    public string LocalTimeText => OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string StatusLabel => CodexChatStatusPolicy.GetLabel(Status);
    public System.Windows.Media.Brush StatusBrush =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(
            CodexChatStatusPolicy.GetColor(Status))!;
    public bool IsWorking => Status is CodexChatStatus.Starting or CodexChatStatus.Working;
    public string StatusSummary => IsUnread ? $"{StatusLabel} · UNREAD" : StatusLabel;
    public string? UsageToolTip => IsLatestForThread
        ? CodexUsageFormatter.FormatThreadToolTip(Usage)
        : null;
    public string DetailUsageText => CodexUsageFormatter.FormatThreadSummary(Usage);
    public string DetailUsageToolTip =>
        CodexUsageFormatter.FormatThreadToolTip(Usage) ??
        "Context-window usage is unavailable from local session telemetry.";
    public string WeeklyUsageText => CodexUsageFormatter.FormatWeeklySummary(WeeklyUsage);
    public string WeeklyUsageToolTip => CodexUsageFormatter.FormatWeeklyToolTip(WeeklyUsage);
    public GitRepositorySnapshot? GitRepository
    {
        get => _gitRepository;
        set
        {
            if (SetField(ref _gitRepository, value))
            {
                OnPropertyChanged(nameof(GitHeaderText));
                OnPropertyChanged(nameof(GitToolTip));
                OnPropertyChanged(nameof(CanCommitAndPush));
                OnPropertyChanged(nameof(CanRefreshGit));
            }
        }
    }

    public bool IsGitRefreshing
    {
        get => _isGitRefreshing;
        set
        {
            if (SetField(ref _isGitRefreshing, value))
            {
                OnPropertyChanged(nameof(GitHeaderText));
                OnPropertyChanged(nameof(GitToolTip));
                OnPropertyChanged(nameof(CanCommitAndPush));
                OnPropertyChanged(nameof(CanRefreshGit));
            }
        }
    }

    public string GitHeaderText => GitRepositoryFormatter.FormatHeader(GitRepository, IsGitRefreshing);
    public string GitToolTip => GitRepositoryFormatter.FormatToolTip(GitRepository, IsGitRefreshing);
    public bool CanCommitAndPush => !IsGitRefreshing && GitRepository?.CanCommitAndPush is true;
    public bool CanRefreshGit => !IsGitRefreshing;
    public ReferencedFileResolution? ReferencedFileReference { get; init; }
    public string? ReferencedFilePath => ReferencedFileReference?.Path;
    public bool IsReferencedFileAvailable =>
        !string.IsNullOrWhiteSpace(ReferencedFilePath) &&
        System.IO.File.Exists(ReferencedFilePath);
    public string ReferencedFileOpenToolTip =>
        ReferencedFileToolTipFormatter.FormatAutomaticOpen(ReferencedFileReference);

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    internal void RefreshUsagePresentation()
    {
        OnPropertyChanged(nameof(UsageToolTip));
        OnPropertyChanged(nameof(WeeklyUsageToolTip));
    }

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
