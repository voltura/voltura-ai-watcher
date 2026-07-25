namespace VolturaAiWatcher;

public partial class CodexNotificationWindow : System.Windows.Window, System.IDisposable
{
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosNoSize = 0x0001;
    private readonly System.Func<CodexMessageEntry, System.Threading.Tasks.Task> _openMessage;
    private readonly System.Windows.Threading.DispatcherTimer _dismissTimer;
    private bool _allowClose;

    public CodexNotificationWindow(System.Func<CodexMessageEntry, System.Threading.Tasks.Task> openMessage)
    {
        InitializeComponent();
        _openMessage = openMessage;
        _dismissTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(8)
        };
        _dismissTimer.Tick += (_, _) => Dismiss();
        SourceInitialized += (_, _) => PositionAboveTaskbar();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        System.IntPtr windowHandle,
        System.IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(System.IntPtr windowHandle, out NativeRect rect);

    public void ShowMessage(CodexMessageEntry entry)
    {
        DataContext = entry;
        _dismissTimer.Stop();
        if (!IsVisible)
        {
            Show();
        }

        PositionAboveTaskbar();
        PanelShell.BeginAnimation(
            System.Windows.UIElement.OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.42,
                To = 1,
                Duration = System.TimeSpan.FromMilliseconds(170)
            });
        _dismissTimer.Start();
    }

    public void RefreshForThread(string threadId)
    {
        if (DataContext is CodexMessageEntry entry &&
            string.Equals(entry.ThreadId, threadId, System.StringComparison.Ordinal))
        {
            _dismissTimer.Stop();
            _dismissTimer.Start();
        }
    }

    public void Dismiss()
    {
        _dismissTimer.Stop();
        Hide();
    }

    private void PositionAboveTaskbar()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == System.IntPtr.Zero)
        {
            return;
        }

        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }

        if (!GetWindowRect(handle, out var rect))
        {
            return;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var x = screen.WorkingArea.Right - width - 12;
        var y = screen.WorkingArea.Bottom - height - 12;
        SetWindowPos(
            handle,
            new System.IntPtr(-1),
            x,
            y,
            0,
            0,
            SetWindowPosNoSize | SetWindowPosNoActivate);
    }

    private async void Message_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left &&
            e.ClickCount == 1 &&
            DataContext is CodexMessageEntry entry)
        {
            e.Handled = true;
            Dismiss();
            await _openMessage(entry);
        }
    }

    private void Dismiss_Click(object sender, System.Windows.RoutedEventArgs e) => Dismiss();

    private void OpenReferencedFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CodexMessageEntry entry && ReferencedFileActions.Open(entry))
        {
            Dismiss();
        }
    }

    private void CopyReferencedFilePath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CodexMessageEntry entry && ReferencedFileActions.CopyPath(entry))
        {
            Dismiss();
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Dismiss();
    }

    public void Dispose()
    {
        _dismissTimer.Stop();
        _allowClose = true;
        Close();
    }
}
