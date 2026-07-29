namespace VolturaAiWatcher;

public static class ReferencedFileActions
{
    private const int ErrorNoAssociation = 1155;

    public static bool Open(CodexMessageEntry entry)
    {
        return OpenPath(entry.ReferencedFilePath);
    }

    public static bool OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
        {
            return false;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (RequiresOpenWith(ex.NativeErrorCode))
        {
            return ShowOpenWithDialog(path);
        }
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Could not open referenced file '{path}': {ex}");
            return false;
        }
    }

    public static bool OpenWebLink(string? address)
    {
        if (!System.Uri.TryCreate(address, System.UriKind.Absolute, out var uri) ||
            (uri.Scheme != System.Uri.UriSchemeHttp &&
             uri.Scheme != System.Uri.UriSchemeHttps))
        {
            return false;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            return true;
        }
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Could not open web link '{uri.AbsoluteUri}': {ex}");
            return false;
        }
    }

    public static bool CopyPath(CodexMessageEntry entry)
    {
        if (!TryGetAvailablePath(entry, out var path))
        {
            return false;
        }

        try
        {
            System.Windows.Clipboard.SetText(FormatClipboardPath(path));
            return true;
        }
        catch (System.Exception ex) when (
            ex is System.Runtime.InteropServices.COMException or
                System.Threading.ThreadStateException)
        {
            App.WriteStartupLog($"Could not copy referenced file path '{path}': {ex}");
            return false;
        }
    }

    public static bool CopyFile(CodexMessageEntry entry)
    {
        if (!TryGetAvailablePath(entry, out var path))
        {
            return false;
        }

        try
        {
            var files = new System.Collections.Specialized.StringCollection { path };
            var data = new System.Windows.DataObject();
            data.SetFileDropList(files);
            data.SetData(
                "Preferred DropEffect",
                new System.IO.MemoryStream(System.BitConverter.GetBytes(1u), writable: false));
            System.Windows.Clipboard.SetDataObject(data, copy: true);
            return true;
        }
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Could not copy referenced file '{path}': {ex}");
            return false;
        }
    }

    public static string FormatClipboardPath(string path) =>
        path.Contains(' ')
            ? $"\"{path}\""
            : path;

    internal static bool RequiresOpenWith(int nativeErrorCode) =>
        nativeErrorCode == ErrorNoAssociation;

    private static bool ShowOpenWithDialog(string path)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "rundll32.exe",
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add("shell32.dll,OpenAs_RunDLL");
            startInfo.ArgumentList.Add(path);
            System.Diagnostics.Process.Start(startInfo);
            return true;
        }
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Could not show Open With for referenced file '{path}': {ex}");
            return false;
        }
    }

    private static bool TryGetAvailablePath(
        CodexMessageEntry entry,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? path)
    {
        path = entry.ReferencedFilePath;
        return !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path);
    }
}
