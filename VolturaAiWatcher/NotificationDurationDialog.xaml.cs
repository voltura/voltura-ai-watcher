namespace VolturaAiWatcher;

public partial class NotificationDurationDialog : System.Windows.Window
{
    private const uint SetWindowPosNoActivate = 0x0010;
    private readonly System.Drawing.Point _screenAnchor;

    public NotificationDurationDialog(int currentSeconds, System.Drawing.Point screenAnchor)
    {
        InitializeComponent();
        _screenAnchor = screenAnchor;
        SecondsBox.Text = NotificationDurationPolicy.NormalizeCustom(currentSeconds).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        System.Windows.DataObject.AddPastingHandler(SecondsBox, SecondsBox_Pasting);
        SourceInitialized += PositionNearAnchor;
        Loaded += (_, _) =>
        {
            SecondsBox.Focus();
            SecondsBox.SelectAll();
        };
    }

    public int DurationSeconds { get; private set; }

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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr windowHandle);

    private void PositionNearAnchor(object? sender, System.EventArgs e)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == System.IntPtr.Zero)
        {
            return;
        }

        var dpi = GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var width = (int)System.Math.Ceiling(Width * scale);
        var height = (int)System.Math.Ceiling(Height * scale);
        var workingArea = System.Windows.Forms.Screen.FromPoint(_screenAnchor).WorkingArea;
        var x = System.Math.Clamp(_screenAnchor.X - width, workingArea.Left, workingArea.Right - width);
        var y = System.Math.Clamp(_screenAnchor.Y - height, workingArea.Top, workingArea.Bottom - height);
        SetWindowPos(
            handle,
            new System.IntPtr(-1),
            x,
            y,
            width,
            height,
            SetWindowPosNoActivate);
    }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!int.TryParse(
                SecondsBox.Text,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var seconds))
        {
            StatusText.Text = "ENTER A NUMBER // 1-600";
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0x5A, 0x73));
            return;
        }

        DurationSeconds = NotificationDurationPolicy.NormalizeCustom(seconds);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;

    private void SecondsBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) =>
        e.Handled = e.Text.Any(character => !char.IsDigit(character));

    private static void SecondsBox_Pasting(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText)
            || e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) is not string text
            || text.Any(character => !char.IsDigit(character)))
        {
            e.CancelCommand();
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key is System.Windows.Input.Key.Enter)
        {
            Confirm_Click(this, new System.Windows.RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void Shell_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton is System.Windows.Input.MouseButton.Left
            && e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            DragMove();
        }
    }
}
