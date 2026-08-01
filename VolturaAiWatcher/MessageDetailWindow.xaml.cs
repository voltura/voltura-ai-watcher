namespace VolturaAiWatcher;

public partial class MessageDetailWindow : System.Windows.Window
{
    private readonly System.Collections.Generic.IReadOnlyList<CodexMessageEntry> _entries;
    private readonly System.Func<CodexMessageEntry, System.Threading.Tasks.Task> _openMessage;
    private readonly GitRepositoryService _gitRepositoryService;
    private System.Threading.CancellationTokenSource? _gitRefreshCancellation;
    private int _index;
    private CodexMessageEntry _entry;

    public MessageDetailWindow(
        CodexMessageEntry entry,
        System.Collections.Generic.IReadOnlyList<CodexMessageEntry> entries,
        System.Func<CodexMessageEntry, System.Threading.Tasks.Task> openMessage,
        GitRepositoryService gitRepositoryService)
    {
        InitializeComponent();
        _entry = entry;
        _entries = entries.Count > 0 ? entries : [entry];
        _index = FindEntryIndex(entry);
        _openMessage = openMessage;
        _gitRepositoryService = gitRepositoryService;
        Closed += (_, _) => CancelGitRefresh();
        ShowCurrentEntry();
    }

    private int FindEntryIndex(CodexMessageEntry entry)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (ReferenceEquals(_entries[index], entry) ||
                string.Equals(_entries[index].Id, entry.Id, System.StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private void ShowCurrentEntry()
    {
        _entry = _entries[_index];
        _entry.IsUnread = false;
        DataContext = _entry;
        MessageBodyViewer.Document = MessageDocumentBuilder.Build(
            _entry.DisplayText,
            _entry.WorkingDirectory);
        PreviousButton.IsEnabled = MessageNavigationPolicy.CanOpenPrevious(_index, _entries.Count);
        NextButton.IsEnabled = MessageNavigationPolicy.CanOpenNext(_index);
        _ = RefreshGitStatusAsync();
    }

    private void Previous_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (MessageNavigationPolicy.CanOpenPrevious(_index, _entries.Count))
        {
            _index = MessageNavigationPolicy.GetPreviousIndex(_index);
            ShowCurrentEntry();
        }
    }

    private void Next_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (MessageNavigationPolicy.CanOpenNext(_index))
        {
            _index = MessageNavigationPolicy.GetNextIndex(_index);
            ShowCurrentEntry();
        }
    }

    private void Copy_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        CopyToClipboard(_entry.Text);
    }

    private void CopyFormatted_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        CopyToClipboard(_entry.DisplayText);
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }

    private void OpenReferencedFile_Click(object sender, System.Windows.RoutedEventArgs e) =>
        ReferencedFileActions.Open(_entry);

    private void CopyReferencedFile_Click(object sender, System.Windows.RoutedEventArgs e) =>
        ReferencedFileActions.CopyFile(_entry);

    private void CopyReferencedFilePath_Click(object sender, System.Windows.RoutedEventArgs e) =>
        ReferencedFileActions.CopyPath(_entry);

    private async void OpenCodex_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await _openMessage(_entry);
        Close();
    }

    private async void ReviewChanges_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_entry.GitRepository is not
            {
                IsRepository: true,
                HasChanges: true,
                Error: null
            } snapshot ||
            string.IsNullOrWhiteSpace(snapshot.RepositoryRoot))
        {
            return;
        }

        var opened = await CodexWindowActivator.OpenNewChatAsync(snapshot.RepositoryRoot, "/review");
        if (opened)
        {
            Close();
        }
        else
        {
            App.WriteStartupLog("Codex review chat could not be opened.");
        }
    }

    private async void RefreshGit_Click(object sender, System.Windows.RoutedEventArgs e) =>
        await RefreshGitStatusAsync();

    private async System.Threading.Tasks.Task RefreshGitStatusAsync()
    {
        CancelGitRefresh();
        var cancellation = new System.Threading.CancellationTokenSource();
        _gitRefreshCancellation = cancellation;
        var entry = _entry;
        entry.IsGitRefreshing = true;
        try
        {
            var snapshot = await _gitRepositoryService.GetSnapshotAsync(
                entry.WorkingDirectory,
                cancellation.Token);
            if (!cancellation.IsCancellationRequested && ReferenceEquals(entry, _entry))
            {
                entry.GitRepository = snapshot;
            }
        }
        catch (System.OperationCanceledException)
        {
        }
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Git status refresh failed: {ex.GetType().Name}: {ex.Message}");
            if (ReferenceEquals(entry, _entry))
            {
                entry.GitRepository = GitRepositorySnapshot.Unavailable("Repository status could not be refreshed.");
            }
        }
        finally
        {
            entry.IsGitRefreshing = false;
            if (ReferenceEquals(_gitRefreshCancellation, cancellation))
            {
                _gitRefreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async void CommitPush_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_entry.GitRepository is not { CanCommitAndPush: true } snapshot)
        {
            return;
        }

        var dialog = new GitCommitPushDialog(
            snapshot,
            GitCommitMessageFormatter.CreateDefault(_entry.ProjectName, _entry.ChatTitle),
            _gitRepositoryService)
        {
            Owner = this
        };
        if (dialog.ShowDialog() is true)
        {
            await RefreshGitStatusAsync();
        }
    }

    private void CancelGitRefresh()
    {
        _gitRefreshCancellation?.Cancel();
        _gitRefreshCancellation = null;
    }

    private void Close_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left ||
            e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase ||
            e.OriginalSource is System.Windows.Controls.Primitives.ScrollBar ||
            e.OriginalSource is System.Windows.Controls.Primitives.Thumb)
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
}
