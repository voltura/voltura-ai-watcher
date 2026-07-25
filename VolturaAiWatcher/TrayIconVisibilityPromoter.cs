namespace VolturaAiWatcher;

internal static class TrayIconVisibilityPromoter
{
    private const int RetryIntervalMilliseconds = 250;
    private const int RetryLimit = 20;
    private const string NotifyIconSettingsSubKey = @"Control Panel\NotifyIconSettings";

    public static void PromoteWhenReady(
        System.ComponentModel.IContainer components,
        System.Windows.Forms.NotifyIcon icon)
    {
        var attempts = 0;
        var timer = new System.Windows.Forms.Timer(components)
        {
            Interval = RetryIntervalMilliseconds
        };

        timer.Tick += (_, _) =>
        {
            attempts++;
            if (!TryPromoteCurrentProcess(out var changed) && attempts < RetryLimit)
            {
                return;
            }

            timer.Stop();
            if (changed)
            {
                icon.Visible = false;
                icon.Visible = true;
            }
        };
        timer.Start();
    }

    private static bool TryPromoteCurrentProcess(out bool changed)
    {
        changed = false;
        if (System.Environment.ProcessPath is not { Length: > 0 } path)
        {
            return true;
        }

        try
        {
            using var root = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                NotifyIconSettingsSubKey,
                writable: true);
            return root is null || TryPromoteEntries(root, path, out changed);
        }
        catch
        {
            return true;
        }
    }

    internal static bool TryPromoteEntries(
        Microsoft.Win32.RegistryKey root,
        string path,
        out bool changed)
    {
        changed = false;
        var matched = false;
        var normalized = NormalizePath(path);

        foreach (var name in root.GetSubKeyNames())
        {
            using var entry = root.OpenSubKey(name, writable: true);
            if (entry?.GetValue("ExecutablePath") is not string candidate ||
                !string.Equals(
                    normalized,
                    NormalizePath(candidate),
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matched = true;
            if (!Equals(entry.GetValue("IsPromoted"), 1))
            {
                entry.SetValue("IsPromoted", 1, Microsoft.Win32.RegistryValueKind.DWord);
                changed = true;
            }
        }

        return matched;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path).TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);
        }
        catch (System.Exception ex) when (
            ex is System.ArgumentException or
                System.NotSupportedException or
                System.IO.PathTooLongException)
        {
            return path;
        }
    }
}
