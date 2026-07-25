namespace VolturaAiWatcher;

public partial class MessageDetailWindow : System.Windows.Window
{
    private readonly System.Collections.Generic.IReadOnlyList<CodexMessageEntry> _entries;
    private readonly System.Func<CodexMessageEntry, System.Threading.Tasks.Task> _openMessage;
    private int _index;
    private CodexMessageEntry _entry;

    public MessageDetailWindow(
        CodexMessageEntry entry,
        System.Collections.Generic.IReadOnlyList<CodexMessageEntry> entries,
        System.Func<CodexMessageEntry, System.Threading.Tasks.Task> openMessage)
    {
        InitializeComponent();
        _entry = entry;
        _entries = entries.Count > 0 ? entries : [entry];
        _index = FindEntryIndex(entry);
        _openMessage = openMessage;
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
        PreviousButton.IsEnabled = _index < _entries.Count - 1;
        NextButton.IsEnabled = _index > 0;
    }

    private void Previous_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_index < _entries.Count - 1)
        {
            _index++;
            ShowCurrentEntry();
        }
    }

    private void Next_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_index > 0)
        {
            _index--;
            ShowCurrentEntry();
        }
    }

    private void Copy_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(_entry.Text);
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
