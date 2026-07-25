namespace VolturaAiWatcher;

public sealed class CodexMessageEntry : System.ComponentModel.INotifyPropertyChanged
{
    private string _chatTitle = "Untitled chat";
    private CodexChatStatus _status;
    private bool _isUnread;
    private bool _isCleared;
    private bool _isLatestForThread;

    public required string Id { get; init; }
    public required string ThreadId { get; init; }
    public required string ProjectName { get; init; }
    public string? WorkingDirectory { get; init; }
    public required string Sender { get; init; }
    public required string Text { get; init; }
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
        set => SetField(ref _isLatestForThread, value);
    }

    public string HeaderText => $"{Sender.ToUpperInvariant()} // {ProjectName.ToUpperInvariant()} // {ChatTitle.ToUpperInvariant()}";
    public string LocalTimeText => OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string StatusLabel => CodexChatStatusPolicy.GetLabel(Status);
    public System.Windows.Media.Brush StatusBrush =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(
            CodexChatStatusPolicy.GetColor(Status))!;
    public bool IsWorking => Status is CodexChatStatus.Starting or CodexChatStatus.Working;
    public string StatusSummary => IsUnread ? $"{StatusLabel} · UNREAD" : StatusLabel;
    public string? ReferencedFilePath { get; init; }
    public bool IsReferencedFileAvailable =>
        !string.IsNullOrWhiteSpace(ReferencedFilePath) &&
        System.IO.File.Exists(ReferencedFilePath);

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

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
