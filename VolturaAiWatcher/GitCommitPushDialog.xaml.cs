namespace VolturaAiWatcher;

public partial class GitCommitPushDialog : System.Windows.Window, System.ComponentModel.INotifyPropertyChanged
{
    private readonly GitRepositoryService _service;
    private readonly GitRepositorySnapshot _snapshot;
    private string _commitMessage;
    private string _operationStatus = "Review the repository and confirm to stage all changes, commit, and push.";
    private System.Windows.Media.Brush _operationStatusBrush =
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#74D98A")!;
    private bool _isRunning;

    public GitCommitPushDialog(
        GitRepositorySnapshot snapshot,
        string defaultCommitMessage,
        GitRepositoryService service)
    {
        _snapshot = snapshot;
        _service = service;
        _commitMessage = defaultCommitMessage;
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            CommitMessageTextBox.Focus();
            CommitMessageTextBox.SelectAll();
        };
    }

    public string RepositoryRoot => _snapshot.RepositoryRoot ?? string.Empty;
    public string BranchSummary => $"{_snapshot.HeadLabel}  →  {_snapshot.Upstream}";
    public string ChangeSummary =>
        $"{_snapshot.ChangedFiles} changed / {_snapshot.NewFiles} new / {_snapshot.DeletedFiles} deleted  //  " +
        $"tracked +{_snapshot.TrackedAddedLines}/-{_snapshot.TrackedRemovedLines} rows  //  " +
        $"untracked +{_snapshot.UntrackedAddedLines} estimated rows";
    public string RepositoryToolTip => GitRepositoryFormatter.FormatToolTip(_snapshot, isRefreshing: false);

    public string CommitMessage
    {
        get => _commitMessage;
        set
        {
            if (_commitMessage == value)
            {
                return;
            }

            _commitMessage = value;
            OnPropertyChanged(nameof(CommitMessage));
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public string OperationStatus
    {
        get => _operationStatus;
        private set
        {
            _operationStatus = value;
            OnPropertyChanged(nameof(OperationStatus));
        }
    }

    public System.Windows.Media.Brush OperationStatusBrush
    {
        get => _operationStatusBrush;
        private set
        {
            _operationStatusBrush = value;
            OnPropertyChanged(nameof(OperationStatusBrush));
        }
    }

    public bool CanEdit => !_isRunning;
    public bool CanConfirm => !_isRunning && !string.IsNullOrWhiteSpace(CommitMessage);

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private async void Confirm_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!CanConfirm)
        {
            return;
        }

        _isRunning = true;
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanConfirm));
        OperationStatus = "Revalidating, staging, committing, and pushing...";
        OperationStatusBrush = Brush("#7CFF9A");

        var result = await _service.CommitAndPushAsync(_snapshot, CommitMessage);
        if (result.Succeeded)
        {
            OperationStatus = result.Message;
            OperationStatusBrush = Brush("#7CFF9A");
            DialogResult = true;
            return;
        }

        _isRunning = false;
        OperationStatus = result.Message;
        OperationStatusBrush = Brush("#FF7C91");
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanConfirm));
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            DialogResult = false;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left ||
            e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase ||
            e.OriginalSource is System.Windows.Controls.TextBox)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (System.InvalidOperationException)
        {
        }
    }

    private static System.Windows.Media.Brush Brush(string value) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(value)!;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
