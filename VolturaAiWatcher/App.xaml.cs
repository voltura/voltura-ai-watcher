namespace VolturaAiWatcher;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\VolturaAiWatcher.SingleInstance";
    private const string ShowExistingEventName = @"Local\VolturaAiWatcher.ShowExisting";
    private static readonly string StartupLogPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "VolturaAiWatcher",
        "startup.log");

    private MainWindow? _mainWindow;
    private System.Threading.Mutex? _singleInstanceMutex;
    private System.Threading.EventWaitHandle? _showExistingEvent;
    private System.Threading.RegisteredWaitHandle? _showExistingRegistration;
    private bool _ownsSingleInstanceMutex;

    private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
    {
        if (!TryBecomeSingleInstance())
        {
            WriteStartupLog("Existing instance signaled.");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            WriteStartupLog(args.Exception.ToString());
            args.Handled = true;
        };

        try
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            ListenForShowRequests();
            _mainWindow.ShowForStartup();
            WriteStartupLog($"Startup OK. Codex home: {_mainWindow.CodexHome}");
        }
        catch (System.Exception ex)
        {
            WriteStartupLog(ex.ToString());
            throw;
        }
    }

    private void Application_Exit(object sender, System.Windows.ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        _showExistingRegistration?.Unregister(null);
        _showExistingRegistration = null;
        _showExistingEvent?.Dispose();
        _showExistingEvent = null;

        if (_ownsSingleInstanceMutex)
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            catch (System.ApplicationException)
            {
            }
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
    }

    private bool TryBecomeSingleInstance()
    {
        _showExistingEvent = new System.Threading.EventWaitHandle(
            false,
            System.Threading.EventResetMode.AutoReset,
            ShowExistingEventName);
        _singleInstanceMutex = new System.Threading.Mutex(false, SingleInstanceMutexName);

        try
        {
            _ownsSingleInstanceMutex = _singleInstanceMutex.WaitOne(0, false);
        }
        catch (System.Threading.AbandonedMutexException)
        {
            _ownsSingleInstanceMutex = true;
        }

        if (_ownsSingleInstanceMutex)
        {
            return true;
        }

        _showExistingEvent.Set();
        _showExistingEvent.Dispose();
        _showExistingEvent = null;
        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    private void ListenForShowRequests()
    {
        if (_showExistingEvent is null)
        {
            return;
        }

        _showExistingRegistration = System.Threading.ThreadPool.RegisterWaitForSingleObject(
            _showExistingEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    Dispatcher.BeginInvoke(new System.Action(() => _mainWindow?.ShowFromTray()));
                }
            },
            null,
            System.Threading.Timeout.Infinite,
            executeOnlyOnce: false);
    }

    internal static void WriteStartupLog(string text)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(StartupLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            System.IO.File.AppendAllText(
                StartupLogPath,
                $"[{System.DateTimeOffset.Now:O}] {text}{System.Environment.NewLine}");
        }
        catch
        {
        }
    }
}
