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
        MessageBodyViewer.Document = MessageDocumentBuilder.Build(
            _entry.DisplayText,
            _entry.WorkingDirectory);
        PreviousButton.IsEnabled = MessageNavigationPolicy.CanOpenPrevious(_index, _entries.Count);
        NextButton.IsEnabled = MessageNavigationPolicy.CanOpenNext(_index);
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
