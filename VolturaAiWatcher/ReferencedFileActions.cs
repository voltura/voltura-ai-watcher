namespace VolturaAiWatcher;

public static class ReferencedFileActions
{
    public static bool Open(CodexMessageEntry entry)
    {
        if (!TryGetAvailablePath(entry, out var path))
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
        catch (System.Exception ex)
        {
            App.WriteStartupLog($"Could not open referenced file '{path}': {ex}");
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

    private static bool TryGetAvailablePath(
        CodexMessageEntry entry,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? path)
    {
        path = entry.ReferencedFilePath;
        return !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path);
    }
}
