namespace VolturaAiWatcher;

public partial class MainWindow : System.Windows.Window, System.ComponentModel.INotifyPropertyChanged, System.IDisposable
{
    private const int MaximumMessages = 40;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint PlaySoundAsync = 0x0001;
    private const uint PlaySoundNoDefault = 0x0002;
    private const uint PlaySoundFilename = 0x00020000;
    private static readonly string SettingsPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "VolturaAiWatcher",
        "settings.json");

    private readonly System.Collections.ObjectModel.ObservableCollection<CodexMessageEntry> _messages = [];
    private readonly System.Collections.ObjectModel.ObservableCollection<ChatFilterOption> _chatFilters = [];
    private readonly System.ComponentModel.ICollectionView _messagesView;
    private readonly System.Collections.Generic.HashSet<string> _knownMessageIds = new(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, CodexChatStatus> _threadStatuses =
        new(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, CodexMessageEntry> _latestByThread =
        new(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, CodexUsageSnapshot> _usageByThread =
        new(System.StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<string> _unreadThreads = new(System.StringComparer.Ordinal);
    private readonly System.ComponentModel.IContainer _trayComponents = new System.ComponentModel.Container();
    private readonly CodexSessionMonitor _monitor;
    private readonly GitRepositoryService _gitRepositoryService = new();
    private readonly CodexNotificationWindow _notificationWindow;
    private readonly System.Windows.Threading.DispatcherTimer _usageAgeTimer;
    private readonly string _sparkSoundPath;
    private AppSettings _settings;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _trayIcon;
    private System.Windows.Forms.ToolStripMenuItem? _startMinimizedMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _startWithWindowsMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _showClearedMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _playSoundMenuItem;
    private bool _initialized;
    private bool _initialPlacementApplied;
    private bool _updatingStartupMenu;
    private bool _monitoringPaused;
    private bool _allowClose;
    private bool _disposed;
    private WindowTuckState _tuckState = WindowTuckState.Expanded;
    private string? _tuckMonitorDeviceName;
    private string _footerStatus = "Initializing Codex monitor...";
    private string _visibleCountText = "0 MESSAGES";
    private string _activeChatFilterText = "ALL CHATS";
    private string? _activeChatThreadId;
    private CodexUsageSnapshot? _latestWeeklyUsage;

    public MainWindow()
    {
        InitializeComponent();
        _settings = LoadSettings();
        _settings.NotificationDurationSeconds = NotificationDurationPolicy.NormalizePersisted(
            _settings.NotificationDurationSeconds);
        _settings.MinimizedMessageClickAction = MinimizedMessageClickActionPolicy.NormalizePersisted(
            _settings.MinimizedMessageClickAction);
        _sparkSoundPath = System.IO.Path.Combine(
            System.AppContext.BaseDirectory,
            "Assets",
            "electric-spark.wav");
        _messagesView = System.Windows.Data.CollectionViewSource.GetDefaultView(_messages);
        _messagesView.Filter = FilterMessage;
        _messagesView.SortDescriptions.Add(
            new System.ComponentModel.SortDescription(
                nameof(CodexMessageEntry.OccurredAt),
                System.ComponentModel.ListSortDirection.Descending));
        _chatFilters.Add(new ChatFilterOption(null, "All chats", string.Empty, isSelected: true));

        _monitor = new CodexSessionMonitor();
        _monitor.MessageObserved += Monitor_MessageObserved;
        _monitor.StatusObserved += Monitor_StatusObserved;
        _monitor.UsageObserved += Monitor_UsageObserved;
        _monitor.TitleObserved += Monitor_TitleObserved;
        _monitor.UnreadThreadsChanged += Monitor_UnreadThreadsChanged;
        _monitor.ProjectMetadataChanged += Monitor_ProjectMetadataChanged;
        _monitor.MonitorWarning += Monitor_MonitorWarning;
        _notificationWindow = new CodexNotificationWindow(
            ShowMessageDetails,
            OpenMessageAsync,
            OpenMessageFromContextMenuAsync);
        _usageAgeTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(30)
        };
        _usageAgeTimer.Tick += (_, _) => RefreshUsagePresentation();
        _usageAgeTimer.Start();

        DataContext = this;
        UpdateSoundIcon();
        UpdateSideTuckVisual();
        CreateTrayIcon();
        SourceInitialized += (_, _) => ApplyInitialPlacement();
        Activated += (_, _) => EnforceTopmost();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    public string CodexHome => _monitor.CodexHome;
    public System.ComponentModel.ICollectionView MessagesView => _messagesView;
    public System.Collections.ObjectModel.ObservableCollection<ChatFilterOption> ChatFilters => _chatFilters;

    public string FooterStatus
    {
        get => _footerStatus;
        private set => SetField(ref _footerStatus, value);
    }

    public string VisibleCountText
    {
        get => _visibleCountText;
        private set => SetField(ref _visibleCountText, value);
    }

    public string ActiveChatFilterText
    {
        get => _activeChatFilterText;
        private set => SetField(ref _activeChatFilterText, value);
    }

    public string WeeklyUsageText => CodexUsageFormatter.FormatWeeklySummary(_latestWeeklyUsage);
    public string WeeklyUsageToolTip => CodexUsageFormatter.FormatWeeklyToolTip(_latestWeeklyUsage);

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr windowHandle);

    [System.Runtime.InteropServices.DllImport(
        "winmm.dll",
        EntryPoint = "PlaySoundW",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode,
        SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PlaySound(
        string? soundPath,
        System.IntPtr moduleHandle,
        uint flags);

    public void ShowForStartup()
    {
        _ = InitializeMonitorAsync();
        if (!_settings.StartMinimized)
        {
            ShowFromTray();
        }
    }

    public void ShowFromTray()
    {
        _ = ShowFromTrayAsync();
    }

    private async System.Threading.Tasks.Task ShowFromTrayAsync()
    {
        _notificationWindow.Dismiss();

        if (_tuckState is WindowTuckState.Tucking or WindowTuckState.Restoring)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == System.Windows.WindowState.Minimized)
        {
            WindowState = System.Windows.WindowState.Normal;
        }

        if (_tuckState == WindowTuckState.Tucked)
        {
            await RestoreFromScreenEdgeAsync();
        }
        else
        {
            ApplyInitialPlacement();
        }

        Activate();
        EnforceTopmost();
    }

    private async System.Threading.Tasks.Task InitializeMonitorAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            await _monitor.StartAsync();
            await Dispatcher.InvokeAsync(() =>
            {
                FooterStatus = $"Watching {_monitor.SessionsPath}";
                UpdateVisibleCount();
            });
        }
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Monitor startup failed: {ex}");
            await Dispatcher.InvokeAsync(() => FooterStatus = "Codex monitoring could not be started.");
        }
    }

    private void Monitor_MessageObserved(CodexObservedMessage message, bool historical)
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            if (!_knownMessageIds.Add(message.Id))
            {
                return;
            }

            if (_latestByThread.TryGetValue(message.ThreadId, out var previous))
            {
                previous.IsLatestForThread = false;
            }

            var status = _threadStatuses.TryGetValue(message.ThreadId, out var currentStatus)
                ? currentStatus
                : message.Status;
            var entry = new CodexMessageEntry
            {
                Id = message.Id,
                ThreadId = message.ThreadId,
                ProjectName = message.ProjectName,
                ProjectMetadata = message.ProjectMetadata,
                WorkingDirectory = message.WorkingDirectory,
                ChatTitle = message.ChatTitle,
                Sender = message.Sender,
                Text = message.Text,
                OccurredAt = message.OccurredAt,
                Status = status,
                Usage = _usageByThread.TryGetValue(message.ThreadId, out var usage)
                    ? usage
                    : null,
                WeeklyUsage = _latestWeeklyUsage,
                ReferencedFileReference = ReferencedFileResolver.ResolveFirstExistingFileReference(
                    message.Text,
                    message.WorkingDirectory),
                IsUnread = _unreadThreads.Contains(message.ThreadId) || (!historical && message.Sender == "Codex"),
                IsLatestForThread = true
            };

            if (_settings.ClearedThroughUnixMillisecondsByThread.TryGetValue(
                    entry.ThreadId,
                    out var clearedThrough) &&
                entry.OccurredAt.ToUnixTimeMilliseconds() <= clearedThrough)
            {
                entry.IsCleared = true;
            }

            _latestByThread[entry.ThreadId] = entry;
            EnsureChatFilter(entry);
            _messages.Add(entry);
            TrimMessages();
            RefreshView();
            FooterStatus = $"{entry.Sender} activity received from {entry.ChatTitle}.";

            if (!historical && IsEffectivelyMinimized())
            {
                ShowNotification(entry);
            }

        }));
    }

    private void Monitor_ProjectMetadataChanged()
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            foreach (var entry in _messages)
            {
                entry.UpdateProjectMetadata(_monitor.GetProjectMetadata(entry.ThreadId, entry.WorkingDirectory));
            }
        }));
    }

    private void Monitor_UsageObserved(CodexObservedUsage observed)
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            if (_usageByThread.TryGetValue(observed.ThreadId, out var existing) &&
                !CodexUsagePolicy.IsNewer(existing, observed.Usage))
            {
                return;
            }

            _usageByThread[observed.ThreadId] = observed.Usage;
            if (_latestByThread.TryGetValue(observed.ThreadId, out var latest))
            {
                latest.Usage = observed.Usage;
            }

            if (observed.Usage.WeeklyRemainingPercent.HasValue &&
                (_latestWeeklyUsage is null ||
                 observed.Usage.ObservedAt >= _latestWeeklyUsage.ObservedAt))
            {
                _latestWeeklyUsage = observed.Usage;
                foreach (var entry in _latestByThread.Values)
                {
                    entry.WeeklyUsage = _latestWeeklyUsage;
                }

                RefreshUsagePresentation();
            }
        }));
    }

    private void Monitor_StatusObserved(
        string threadId,
        CodexChatStatus status,
        System.DateTimeOffset occurredAt,
        bool historical)
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            var previousStatus = _threadStatuses.TryGetValue(threadId, out var recordedStatus)
                ? recordedStatus
                : CodexChatStatus.Idle;
            _threadStatuses[threadId] = status;
            if (!historical &&
                status != previousStatus &&
                CodexChatStatusPolicy.IsActionable(status))
            {
                PlaySpark();
            }

            if (!_latestByThread.TryGetValue(threadId, out var latest))
            {
                return;
            }

            latest.Status = status;
            var needsAttention = status is
                CodexChatStatus.WaitingForInput or
                CodexChatStatus.WaitingForApproval or
                CodexChatStatus.WaitingForConnector or
                CodexChatStatus.Completed or
                CodexChatStatus.Interrupted or
                CodexChatStatus.Failed;
            if (!historical && needsAttention)
            {
                latest.IsUnread = true;
            }
            else if (status is CodexChatStatus.Starting or CodexChatStatus.Working)
            {
                latest.IsUnread = false;
            }

            if (!historical &&
                needsAttention &&
                IsEffectivelyMinimized())
            {
                ShowNotification(latest);
            }
            else
            {
                _notificationWindow.RefreshForThread(threadId);
            }
            FooterStatus = $"{latest.ChatTitle}: {latest.StatusLabel}.";
        }));
    }

    private void Monitor_TitleObserved(string threadId, string title)
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            foreach (var entry in _messages.Where(entry =>
                         string.Equals(entry.ThreadId, threadId, System.StringComparison.Ordinal)))
            {
                entry.ChatTitle = title;
            }

            var filter = _chatFilters.FirstOrDefault(option =>
                string.Equals(option.ThreadId, threadId, System.StringComparison.Ordinal));
            if (filter is not null)
            {
                filter.DisplayName = title;
                if (filter.IsSelected)
                {
                    ActiveChatFilterText = title.ToUpperInvariant();
                }
            }
        }));
    }

    private void Monitor_UnreadThreadsChanged(System.Collections.Generic.IReadOnlySet<string> unread)
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            _unreadThreads.Clear();
            _unreadThreads.UnionWith(unread);
            foreach (var entry in _latestByThread.Values)
            {
                entry.IsUnread = unread.Contains(entry.ThreadId);
            }
        }));
    }

    private void Monitor_MonitorWarning(string warning)
    {
        App.WriteStartupLog($"Monitor warning: {warning}");
        Dispatcher.BeginInvoke(new System.Action(() => FooterStatus = warning));
    }

    private bool FilterMessage(object item) =>
        item is CodexMessageEntry entry &&
        (_settings.ShowClearedMessages || !entry.IsCleared) &&
        (_activeChatThreadId is null ||
         string.Equals(entry.ThreadId, _activeChatThreadId, System.StringComparison.Ordinal));

    private void EnsureChatFilter(CodexMessageEntry entry)
    {
        var existing = _chatFilters.FirstOrDefault(option =>
            string.Equals(option.ThreadId, entry.ThreadId, System.StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.DisplayName = entry.ChatTitle;
            existing.ProjectName = entry.ProjectName;
            return;
        }

        _chatFilters.Add(new ChatFilterOption(entry.ThreadId, entry.ChatTitle, entry.ProjectName));
    }

    private void ToggleChatFilter_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ChatFilterPopup.IsOpen = !ChatFilterPopup.IsOpen;
    }

    private void ChatFilterOption_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        _activeChatThreadId = button.Tag as string;
        foreach (var option in _chatFilters)
        {
            option.IsSelected = string.Equals(
                option.ThreadId,
                _activeChatThreadId,
                System.StringComparison.Ordinal);
        }

        var selected = _chatFilters.First(option => option.IsSelected);
        ActiveChatFilterText = selected.ThreadId is null
            ? "ALL CHATS"
            : selected.DisplayName.ToUpperInvariant();
        ChatFilterPopup.IsOpen = false;
        RefreshView();
        FooterStatus = selected.ThreadId is null
            ? "Showing all chats."
            : $"Showing {selected.DisplayName}.";
    }

    private void TrimMessages()
    {
        while (_messages.Count > MaximumMessages)
        {
            var oldest = _messages.OrderBy(entry => entry.OccurredAt).First();
            _messages.Remove(oldest);
            _knownMessageIds.Remove(oldest.Id);
            if (_latestByThread.TryGetValue(oldest.ThreadId, out var latest) &&
                ReferenceEquals(latest, oldest))
            {
                _latestByThread.Remove(oldest.ThreadId);
            }
        }
    }

    private void RefreshView()
    {
        _messagesView.Refresh();
        UpdateVisibleCount();
    }

    private void UpdateVisibleCount()
    {
        var count = _messages.Cast<CodexMessageEntry>().Count(FilterMessage);
        VisibleCountText = $"{count} {(count == 1 ? "MESSAGE" : "MESSAGES")}";
    }

    private async System.Threading.Tasks.Task OpenMessageAsync(CodexMessageEntry entry)
    {
        entry.IsUnread = false;
        _unreadThreads.Remove(entry.ThreadId);
        FooterStatus = $"Opening {entry.ChatTitle} in Codex...";
        var opened = await CodexWindowActivator.OpenAsync(entry.ThreadId);
        await Dispatcher.InvokeAsync(() =>
            FooterStatus = opened
                ? $"Opened {entry.ChatTitle}."
                : "Codex could not be brought to the foreground.");
    }

    private async System.Threading.Tasks.Task OpenMessageFromContextMenuAsync(CodexMessageEntry entry)
    {
        await OpenMessageAsync(entry);
        if (IsVisible &&
            WindowState != System.Windows.WindowState.Minimized &&
            ScreenEdgeTuckPolicy.CanStartTuck(_tuckState))
        {
            await TuckToScreenEdgeAsync();
        }
    }

    private void ShowMessageDetails(CodexMessageEntry entry)
    {
        entry.IsUnread = false;
        _unreadThreads.Remove(entry.ThreadId);
        var visibleEntries = _messagesView
            .Cast<CodexMessageEntry>()
            .OrderByDescending(message => message.OccurredAt)
            .ToArray();
        var detail = new MessageDetailWindow(entry, visibleEntries, OpenMessageAsync, _gitRepositoryService);
        if (!IsEffectivelyMinimized())
        {
            detail.Owner = this;
        }

        detail.ShowDialog();
    }

    private void MessageRow_Click(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left ||
            e.ClickCount != 1 ||
            sender is not System.Windows.FrameworkElement { Tag: CodexMessageEntry entry })
        {
            return;
        }

        e.Handled = true;
        MessagesList.SelectedItem = null;
        ShowMessageDetails(entry);
    }

    private static CodexMessageEntry? GetContextMenuEntry(object sender) =>
        sender is System.Windows.Controls.MenuItem { Tag: CodexMessageEntry entry }
            ? entry
            : null;

    private void ShowMessageDetails_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is { } entry)
        {
            ShowMessageDetails(entry);
        }
    }

    private async void OpenMessageInCodex_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is { } entry)
        {
            await OpenMessageFromContextMenuAsync(entry);
        }
    }

    private void OpenReferencedFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is { } entry)
        {
            FooterStatus = ReferencedFileActions.Open(entry)
                ? $"Opened {System.IO.Path.GetFileName(entry.ReferencedFilePath)}."
                : "The referenced file is unavailable.";
        }
    }

    private void CopyReferencedFilePath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetContextMenuEntry(sender) is { } entry)
        {
            FooterStatus = ReferencedFileActions.CopyPath(entry)
                ? "Referenced file path copied."
                : "The referenced file path could not be copied.";
        }
    }

    private async void OpenCodex_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        FooterStatus = "Opening Codex...";
        var opened = await CodexWindowActivator.OpenAsync(null);
        FooterStatus = opened ? "Codex is visible." : "Codex could not be brought to the foreground.";
    }

    private void OpenUsage_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        FooterStatus = "Opening Codex usage...";
        OpenWebPage("https://chatgpt.com/codex/settings/usage");
    }

    private void ToggleSound_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SetPlaySoundEnabled(!_settings.PlaySoundOnMessage, previewWhenEnabled: true);
    }

    private void SetPlaySoundEnabled(bool enabled, bool previewWhenEnabled)
    {
        _settings.PlaySoundOnMessage = enabled;
        if (_playSoundMenuItem is not null && _playSoundMenuItem.Checked != enabled)
        {
            _playSoundMenuItem.Checked = enabled;
        }

        UpdateSoundIcon();
        SaveSettings();
        FooterStatus = enabled
            ? "Sound enabled when Codex needs your action."
            : "Sound muted.";
        if (enabled && previewWhenEnabled)
        {
            PlaySpark(force: true);
        }
    }

    private void UpdateSoundIcon()
    {
        if (SoundIconPath is null)
        {
            return;
        }

        SoundIconPath.Data = System.Windows.Media.Geometry.Parse(
            _settings.PlaySoundOnMessage
                ? "M3,7 H6 L10,3 V13 L6,9 H3 Z M12,6 C13.2,7 13.2,9 12,10"
                : "M3,7 H6 L10,3 V13 L6,9 H3 Z M11.5,5 L15,11 M15,5 L11.5,11");
        SoundIconPath.Stroke = new System.Windows.Media.SolidColorBrush(
            _settings.PlaySoundOnMessage
                ? System.Windows.Media.Color.FromRgb(124, 255, 154)
                : System.Windows.Media.Color.FromRgb(104, 145, 114));
    }

    private void PlaySpark(bool force = false)
    {
        if ((!_settings.PlaySoundOnMessage && !force) ||
            !System.IO.File.Exists(_sparkSoundPath))
        {
            return;
        }

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            if (!PlaySound(
                    _sparkSoundPath,
                    System.IntPtr.Zero,
                    PlaySoundFilename | PlaySoundAsync | PlaySoundNoDefault))
            {
                App.WriteStartupLog(
                    $"Sound playback failed with Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");
            }
        });
    }

    private void ClearResolved_Click(object sender, System.Windows.RoutedEventArgs e) => ClearResolvedMessages();

    private void ClearResolvedMessages()
    {
        var cleared = 0;
        foreach (var threadGroup in _messages
                     .Cast<CodexMessageEntry>()
                     .GroupBy(entry => entry.ThreadId))
        {
            if (CodexChatStatusPolicy.RequiresRetention(GetCurrentThreadStatus(threadGroup.Key)))
            {
                continue;
            }

            var timestamp = threadGroup.Max(entry => entry.OccurredAt.ToUnixTimeMilliseconds());
            if (!_settings.ClearedThroughUnixMillisecondsByThread.TryGetValue(threadGroup.Key, out var existing) ||
                timestamp > existing)
            {
                _settings.ClearedThroughUnixMillisecondsByThread[threadGroup.Key] = timestamp;
            }

            foreach (var entry in threadGroup.Where(entry => !entry.IsCleared))
            {
                entry.IsCleared = true;
                cleared++;
            }
        }

        SaveSettings();
        RefreshView();
        FooterStatus = cleared == 0 ? "No resolved messages to clear." : $"Cleared {cleared} resolved messages.";
    }

    private CodexChatStatus GetCurrentThreadStatus(string threadId)
    {
        if (_threadStatuses.TryGetValue(threadId, out var status))
        {
            return status;
        }

        return _latestByThread.TryGetValue(threadId, out var latest)
            ? latest.Status
            : CodexChatStatus.Unknown;
    }

    private void Hide_Click(object sender, System.Windows.RoutedEventArgs e) => Hide();

    private async void SideTuckButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ScreenEdgeTuckPolicy.CanStartTuck(_tuckState))
        {
            await TuckToScreenEdgeAsync();
        }
        else if (ScreenEdgeTuckPolicy.CanStartRestore(_tuckState))
        {
            await ShowFromTrayAsync();
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left ||
            e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase ||
            FindVisualAncestor<System.Windows.Controls.ListViewItem>(e.OriginalSource as System.Windows.DependencyObject) is not null ||
            FindVisualAncestor<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as System.Windows.DependencyObject) is not null)
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

    private static T? FindVisualAncestor<T>(System.Windows.DependencyObject? current)
        where T : System.Windows.DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void ApplyInitialPlacement()
    {
        if (_initialPlacementApplied)
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }

        if (!GetWindowRect(handle, out var rect))
        {
            return;
        }

        var physicalWidth = rect.Right - rect.Left;
        var physicalHeight = rect.Bottom - rect.Top;
        var x = screen.WorkingArea.Right - physicalWidth;
        var y = screen.WorkingArea.Top + (screen.WorkingArea.Height - physicalHeight) / 2;
        SetWindowPos(
            handle,
            new System.IntPtr(-1),
            x,
            y,
            0,
            0,
            SetWindowPosNoSize | SetWindowPosNoActivate);
        _initialPlacementApplied = true;
    }

    private bool IsEffectivelyMinimized() =>
        ScreenEdgeTuckPolicy.IsMinimizedEquivalent(
            IsVisible,
            WindowState == System.Windows.WindowState.Minimized,
            _tuckState);

    private async System.Threading.Tasks.Task TuckToScreenEdgeAsync()
    {
        if (!ScreenEdgeTuckPolicy.CanStartTuck(_tuckState))
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        if (!GetWindowRect(handle, out var rect))
        {
            return;
        }

        var windowWidth = rect.Right - rect.Left;
        var windowHeight = rect.Bottom - rect.Top;
        var selectedMonitor = SelectMonitorForWindow(rect);
        _tuckMonitorDeviceName = selectedMonitor.DeviceName;
        var target = GetTuckedPosition(selectedMonitor.Bounds, handle, windowHeight);

        _notificationWindow.Dismiss();
        _tuckState = WindowTuckState.Tucking;
        UpdateSideTuckVisual();
        await AnimateWindowPositionAsync(
            handle,
            new NativePoint(rect.Left, rect.Top),
            target);

        OuterShell.Opacity = 0;
        OuterShell.IsHitTestVisible = false;
        _tuckState = WindowTuckState.Tucked;
        UpdateSideTuckVisual();
    }

    private async System.Threading.Tasks.Task RestoreFromScreenEdgeAsync()
    {
        if (!ScreenEdgeTuckPolicy.CanStartRestore(_tuckState))
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        if (!GetWindowRect(handle, out var rect))
        {
            return;
        }

        var selectedMonitor = SelectMonitorForWindow(rect);
        _tuckMonitorDeviceName = selectedMonitor.DeviceName;
        var target = ScreenEdgeTuckPolicy.GetExpandedPosition(
            selectedMonitor.Bounds,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);

        _tuckState = WindowTuckState.Restoring;
        OuterShell.Opacity = 1;
        OuterShell.IsHitTestVisible = true;
        UpdateSideTuckVisual();
        await AnimateWindowPositionAsync(
            handle,
            new NativePoint(rect.Left, rect.Top),
            target);

        _tuckState = WindowTuckState.Expanded;
        UpdateSideTuckVisual();
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, System.EventArgs e)
    {
        if (_tuckState != WindowTuckState.Tucked)
        {
            return;
        }

        Dispatcher.BeginInvoke(new System.Action(() => _ = RepositionTuckedWindowAsync()));
    }

    private async System.Threading.Tasks.Task RepositionTuckedWindowAsync()
    {
        if (_tuckState != WindowTuckState.Tucked)
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        if (!GetWindowRect(handle, out var rect))
        {
            return;
        }

        var selectedMonitor = SelectMonitorForWindow(rect);
        _tuckMonitorDeviceName = selectedMonitor.DeviceName;
        var target = GetTuckedPosition(
            selectedMonitor.Bounds,
            handle,
            rect.Bottom - rect.Top);

        await AnimateWindowPositionAsync(
            handle,
            new NativePoint(rect.Left, rect.Top),
            target);
    }

    private MonitorWorkArea SelectMonitorForWindow(NativeRect rect)
    {
        var monitors = GetMonitorWorkAreas();
        return ScreenEdgeTuckPolicy.SelectMonitor(
            _tuckMonitorDeviceName,
            monitors,
            new NativeBounds(
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top));
    }

    private static MonitorWorkArea[] GetMonitorWorkAreas() =>
        System.Windows.Forms.Screen.AllScreens
            .Select(screen => new MonitorWorkArea(
                screen.DeviceName,
                ToNativeBounds(screen.WorkingArea),
                screen.Primary))
            .ToArray();

    private static NativePoint GetTuckedPosition(
        NativeBounds workArea,
        System.IntPtr handle,
        int windowHeight)
    {
        var dpiScale = System.Math.Max(1, GetDpiForWindow(handle)) / 96.0;
        return ScreenEdgeTuckPolicy.GetTuckedPosition(
            workArea,
            windowHeight,
            ScreenEdgeTuckPolicy.GetTabWidthPixels(dpiScale));
    }

    private System.Threading.Tasks.Task AnimateWindowPositionAsync(
        System.IntPtr handle,
        NativePoint start,
        NativePoint target)
    {
        var completion = new System.Threading.Tasks.TaskCompletionSource(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Render,
            Dispatcher)
        {
            Interval = System.TimeSpan.FromMilliseconds(15)
        };

        timer.Tick += Tick;
        timer.Start();
        return completion.Task;

        void Tick(object? sender, System.EventArgs args)
        {
            var progress = stopwatch.Elapsed.TotalMilliseconds /
                ScreenEdgeTuckPolicy.AnimationDurationMilliseconds;
            var eased = ScreenEdgeTuckPolicy.EaseInOutCubic(progress);
            var x = (int)System.Math.Round(start.X + ((target.X - start.X) * eased));
            var y = (int)System.Math.Round(start.Y + ((target.Y - start.Y) * eased));
            SetWindowPos(
                handle,
                new System.IntPtr(-1),
                x,
                y,
                0,
                0,
                SetWindowPosNoSize | SetWindowPosNoActivate);

            if (progress < 1)
            {
                return;
            }

            timer.Stop();
            timer.Tick -= Tick;
            completion.TrySetResult();
        }
    }

    private void UpdateSideTuckVisual()
    {
        var restoreVisual = _tuckState is WindowTuckState.Tucked or WindowTuckState.Restoring;
        var label = restoreVisual ? "Restore window" : "Tuck to screen edge";
        SideTuckButton.ToolTip = label;
        SideTuckButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, label);
        SideTuckButton.IsEnabled = _tuckState is WindowTuckState.Expanded or WindowTuckState.Tucked;
        MainWindowChrome.ResizeBorderThickness = ScreenEdgeTuckPolicy.ShouldAllowResize(_tuckState)
            ? new System.Windows.Thickness(7)
            : new System.Windows.Thickness(0);
        SideTuckChevron.RenderTransform = new System.Windows.Media.ScaleTransform(
            restoreVisual ? -1 : 1,
            1);
    }

    private static NativeBounds ToNativeBounds(System.Drawing.Rectangle rectangle) =>
        new(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);

    private void EnforceTopmost()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != System.IntPtr.Zero)
        {
            SetWindowPos(
                handle,
                new System.IntPtr(-1),
                0,
                0,
                0,
                0,
                0x0001 | 0x0002 | SetWindowPosNoActivate);
        }
    }

    private void CreateTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new System.Uri("pack://application:,,,/Assets/voltura-ai-watcher.ico"));
        if (resource is null)
        {
            throw new System.InvalidOperationException("The application icon is unavailable.");
        }

        using (resource.Stream)
        using (var sourceIcon = new System.Drawing.Icon(resource.Stream))
        {
            _trayIcon = (System.Drawing.Icon)sourceIcon.Clone();
        }

        var menu = CreateTrayMenu();
        _notifyIcon = new System.Windows.Forms.NotifyIcon(_trayComponents)
        {
            Icon = _trayIcon,
            Text = "Voltura AI Watcher",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ShowFromTray);
            }
        };
        TrayIconVisibilityPromoter.PromoteWhenReady(_trayComponents, _notifyIcon);
    }

    private void ShowNotification(CodexMessageEntry entry)
    {
        if (_settings.NotificationDurationSeconds == NotificationDurationPolicy.Off ||
            !NotificationMessagePolicy.ShouldShow(
                _monitoringPaused,
                entry.Sender,
                _settings.OnlyShowCodexResponseNotifications))
        {
            return;
        }

        _notificationWindow.ShowMessage(
            entry,
            _settings.NotificationDurationSeconds,
            _settings.MinimizedMessageClickAction);
    }

    private System.Windows.Forms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip(_trayComponents);
        ApplyDropDownTheme(menu);

        var show = CreateMenuItem("Show Voltura AI Watcher");
        show.Click += (_, _) => Dispatcher.Invoke(ShowFromTray);
        var openCodex = CreateMenuItem("Open Codex");
        openCodex.Click += async (_, _) => await CodexWindowActivator.OpenAsync(null);
        var toggleMonitoring = CreateMenuItem("Pause monitoring");
        toggleMonitoring.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            _monitoringPaused = !_monitoringPaused;
            toggleMonitoring.Text = _monitoringPaused
                ? "Continue monitoring"
                : "Pause monitoring";
            if (_monitoringPaused)
            {
                _notificationWindow.Dismiss();
            }
        });
        var clear = CreateMenuItem("Clear resolved messages");
        clear.Click += (_, _) => Dispatcher.Invoke(ClearResolvedMessages);

        var settings = CreateMenuItem("Settings");
        ApplyDropDownTheme(settings.DropDown);
        _startMinimizedMenuItem = CreateMenuItem("Start minimized", checkOnClick: true);
        _startMinimizedMenuItem.Checked = _settings.StartMinimized;
        _startMinimizedMenuItem.CheckedChanged += (_, _) =>
        {
            _settings.StartMinimized = _startMinimizedMenuItem.Checked;
            SaveSettings();
        };
        _startWithWindowsMenuItem = CreateMenuItem("Start with Windows", checkOnClick: true);
        _startWithWindowsMenuItem.Checked = StartupRegistration.IsEnabled();
        _startWithWindowsMenuItem.CheckedChanged += (_, _) =>
        {
            if (_updatingStartupMenu)
            {
                return;
            }

            try
            {
                StartupRegistration.SetEnabled(_startWithWindowsMenuItem.Checked);
            }
            catch (System.Exception ex)
            {
                App.WriteStartupLog($"Startup registration failed: {ex}");
                _updatingStartupMenu = true;
                try
                {
                    _startWithWindowsMenuItem.Checked = StartupRegistration.IsEnabled();
                }
                finally
                {
                    _updatingStartupMenu = false;
                }
            }
        };
        _showClearedMenuItem = CreateMenuItem("Show cleared messages", checkOnClick: true);
        _showClearedMenuItem.Checked = _settings.ShowClearedMessages;
        _showClearedMenuItem.CheckedChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.ShowClearedMessages = _showClearedMenuItem.Checked;
            SaveSettings();
            RefreshView();
        });
        _playSoundMenuItem = CreateMenuItem("Play sound when action is needed", checkOnClick: true);
        _playSoundMenuItem.Checked = _settings.PlaySoundOnMessage;
        _playSoundMenuItem.Click += (_, _) => Dispatcher.Invoke(() =>
            SetPlaySoundEnabled(_playSoundMenuItem.Checked, previewWhenEnabled: true));
        var codexResponsesOnly = CreateMenuItem("Only notify for Codex responses", checkOnClick: true);
        codexResponsesOnly.Checked = _settings.OnlyShowCodexResponseNotifications;
        codexResponsesOnly.CheckedChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.OnlyShowCodexResponseNotifications = codexResponsesOnly.Checked;
            if (codexResponsesOnly.Checked)
            {
                _notificationWindow.DismissIfShowingNonCodexResponse();
            }

            SaveSettings();
        });
        var notificationDuration = CreateMenuItem("Notification display time");
        ApplyDropDownTheme(notificationDuration.DropDown);
        var durationItems = new System.Collections.Generic.Dictionary<int, System.Windows.Forms.ToolStripMenuItem>();
        var durationChoices = new (int Seconds, string Label)[]
        {
            (NotificationDurationPolicy.Off, "Off"),
            (5, "5 seconds"),
            (10, "10 seconds"),
            (NotificationDurationPolicy.UntilDismissed, "Until dismissed")
        };
        var customDuration = CreateMenuItem("Custom...");

        void UpdateDurationChecks()
        {
            foreach (var pair in durationItems)
            {
                pair.Value.Checked = pair.Key == _settings.NotificationDurationSeconds;
            }

            customDuration.Checked = !NotificationDurationPolicy.IsPreset(
                _settings.NotificationDurationSeconds);
        }

        foreach (var choice in durationChoices)
        {
            var durationItem = CreateMenuItem(choice.Label);
            durationItem.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                _settings.NotificationDurationSeconds = choice.Seconds;
                UpdateDurationChecks();
                if (choice.Seconds == NotificationDurationPolicy.Off)
                {
                    _notificationWindow.Dismiss();
                }

                SaveSettings();
            });
            durationItems.Add(choice.Seconds, durationItem);
            notificationDuration.DropDownItems.Add(durationItem);
        }

        customDuration.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            var dialog = new NotificationDurationDialog(
                _settings.NotificationDurationSeconds,
                System.Windows.Forms.Cursor.Position);
            if (!IsEffectivelyMinimized())
            {
                dialog.Owner = this;
            }

            if (dialog.ShowDialog() is true)
            {
                _settings.NotificationDurationSeconds = dialog.DurationSeconds;
                UpdateDurationChecks();
                SaveSettings();
            }
        });
        notificationDuration.DropDownItems.Add(customDuration);
        var minimizedMessageClickAction = CreateMenuItem("Minimized message click");
        ApplyDropDownTheme(minimizedMessageClickAction.DropDown);
        var showMessageDetails = CreateMenuItem("Show message details", checkOnClick: true);
        var openMessageInCodex = CreateMenuItem("Open in Codex", checkOnClick: true);

        void UpdateMinimizedMessageClickChecks()
        {
            showMessageDetails.Checked = _settings.MinimizedMessageClickAction ==
                MinimizedMessageClickAction.ShowMessageDetails;
            openMessageInCodex.Checked = _settings.MinimizedMessageClickAction ==
                MinimizedMessageClickAction.OpenInCodex;
        }

        showMessageDetails.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.MinimizedMessageClickAction = MinimizedMessageClickAction.ShowMessageDetails;
            UpdateMinimizedMessageClickChecks();
            SaveSettings();
        });
        openMessageInCodex.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.MinimizedMessageClickAction = MinimizedMessageClickAction.OpenInCodex;
            UpdateMinimizedMessageClickChecks();
            SaveSettings();
        });
        minimizedMessageClickAction.DropDownItems.AddRange([showMessageDetails, openMessageInCodex]);
        UpdateMinimizedMessageClickChecks();
        UpdateDurationChecks();
        settings.DropDownItems.AddRange(
            [
                _startMinimizedMenuItem,
                _startWithWindowsMenuItem,
                _playSoundMenuItem,
                codexResponsesOnly,
                notificationDuration,
                minimizedMessageClickAction,
                _showClearedMenuItem
            ]);

        var about = CreateMenuItem("About");
        ApplyDropDownTheme(about.DropDown);
        var version = CreateMenuItem($"Voltura AI Watcher v{GetProductVersion()}");
        version.Font = new System.Drawing.Font(version.Font, System.Drawing.FontStyle.Bold);
        version.ForeColor = System.Drawing.Color.FromArgb(124, 255, 154);
        version.Padding = new System.Windows.Forms.Padding(8, 5, 10, 5);
        var projectPage = CreateMenuItem("Project page");
        projectPage.Click += (_, _) => OpenWebPage("https://voltura.github.io/voltura-ai-watcher/");
        var latestRelease = CreateMenuItem("Latest release");
        latestRelease.Click += (_, _) => OpenWebPage("https://github.com/voltura/voltura-ai-watcher/releases/latest");
        about.DropDownItems.AddRange([version, projectPage, latestRelease]);

        var exit = CreateMenuItem("Exit");
        exit.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        menu.Items.AddRange(
            [
                show,
                openCodex,
                toggleMonitoring,
                new System.Windows.Forms.ToolStripSeparator(),
                clear,
                settings,
                about,
                new System.Windows.Forms.ToolStripSeparator(),
                exit
            ]);
        return menu;
    }

    private static string GetProductVersion()
    {
        var informationalVersion = System.Reflection.Assembly
            .GetEntryAssembly()?
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(informationalVersion)
            ? "unknown"
            : informationalVersion.Split('+', 2)[0];
    }

    private static void OpenWebPage(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Could not open '{url}': {ex}");
        }
    }

    private static System.Windows.Forms.ToolStripMenuItem CreateMenuItem(string text, bool checkOnClick = false) =>
        new(text)
        {
            CheckOnClick = checkOnClick,
            ForeColor = System.Drawing.Color.FromArgb(124, 255, 154),
            BackColor = System.Drawing.Color.FromArgb(8, 15, 11),
            Font = new System.Drawing.Font("Bahnschrift SemiCondensed", 9f),
            Padding = new System.Windows.Forms.Padding(4, 3, 4, 3)
        };

    private static void ApplyDropDownTheme(System.Windows.Forms.ToolStrip dropDown)
    {
        dropDown.Renderer = new CyberpunkTrayMenuRenderer();
        dropDown.BackColor = System.Drawing.Color.FromArgb(8, 15, 11);
        dropDown.ForeColor = System.Drawing.Color.FromArgb(124, 255, 154);
        dropDown.Font = new System.Drawing.Font("Bahnschrift SemiCondensed", 9f);
        if (dropDown is System.Windows.Forms.ToolStripDropDownMenu menu)
        {
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Padding = new System.Windows.Forms.Padding(3);
        }
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            if (System.IO.File.Exists(SettingsPath))
            {
                var json = System.IO.File.ReadAllText(SettingsPath);
                return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.Text.Json.JsonException)
        {
            App.WriteStartupLog($"Settings load failed: {ex.Message}");
        }

        return new AppSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(SettingsPath)!;
            System.IO.Directory.CreateDirectory(directory);
            var json = System.Text.Json.JsonSerializer.Serialize(
                _settings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(SettingsPath, json);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            App.WriteStartupLog($"Settings save failed: {ex.Message}");
        }
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        return true;
    }

    private void RefreshUsagePresentation()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(WeeklyUsageText)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(WeeklyUsageToolTip)));
        foreach (var entry in _latestByThread.Values)
        {
            entry.RefreshUsagePresentation();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        _usageAgeTimer.Stop();
        _monitor.Dispose();
        PlaySound(null, System.IntPtr.Zero, 0);
        _notificationWindow.Dispose();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
        _trayComponents.Dispose();
    }
}
