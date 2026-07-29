namespace VolturaAiWatcher;

public static class CodexWindowActivator
{
    private const int RestoreWindow = 9;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(System.IntPtr windowHandle, int command);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(System.IntPtr windowHandle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsIconic(System.IntPtr windowHandle);

    public static async System.Threading.Tasks.Task<bool> OpenAsync(string? threadId)
    {
        var existing = FindCodexWindow();
        if (existing != System.IntPtr.Zero)
        {
            RestoreAndFocus(existing);
        }

        if (!string.IsNullOrWhiteSpace(threadId))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"codex://threads/{System.Uri.EscapeDataString(threadId)}",
                    UseShellExecute = true
                });
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }
        else if (existing == System.IntPtr.Zero)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "codex:",
                    UseShellExecute = true
                });
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var handle = FindCodexWindow();
            if (handle != System.IntPtr.Zero)
            {
                RestoreAndFocus(handle);
                return true;
            }

            await System.Threading.Tasks.Task.Delay(100);
        }

        return existing != System.IntPtr.Zero;
    }

    private static System.IntPtr FindCodexWindow()
    {
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var handle = process.MainWindowHandle;
                    if (handle == System.IntPtr.Zero)
                    {
                        continue;
                    }

                    if (string.Equals(process.ProcessName, "ChatGPT", System.StringComparison.OrdinalIgnoreCase) ||
                        process.MainWindowTitle.Contains("Codex", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return handle;
                    }
                }
                catch (System.InvalidOperationException)
                {
                }
            }
        }

        return System.IntPtr.Zero;
    }

    private static void RestoreAndFocus(System.IntPtr handle)
    {
        if (ShouldRestoreWindow(IsIconic(handle)))
        {
            ShowWindowAsync(handle, RestoreWindow);
        }

        SetForegroundWindow(handle);
    }

    internal static bool ShouldRestoreWindow(bool isIconic) => isIconic;
}
