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

    public static System.Threading.Tasks.Task<bool> OpenNewChatAsync(
        string? workspacePath,
        string initialPrompt)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) ||
            !System.IO.Path.IsPathFullyQualified(workspacePath) ||
            !System.IO.Directory.Exists(workspacePath) ||
            string.IsNullOrWhiteSpace(initialPrompt))
        {
            return System.Threading.Tasks.Task.FromResult(false);
        }

        var uri = BuildNewChatUri(workspacePath, initialPrompt);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return System.Threading.Tasks.Task.FromResult(false);
        }

        // A pre-existing Codex window cannot prove that this deep link created
        // or selected the requested repository-scoped chat. Process.Start
        // succeeding is the strongest result available from the shell URI
        // dispatch; the Codex app owns the subsequent navigation.
        return System.Threading.Tasks.Task.FromResult(true);
    }

    internal static string BuildNewChatUri(string workspacePath, string initialPrompt)
    {
        var normalizedPath = System.IO.Path.GetFullPath(workspacePath);
        return $"codex://new?path={System.Uri.EscapeDataString(normalizedPath)}" +
               $"&prompt={System.Uri.EscapeDataString(initialPrompt)}";
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
