namespace VolturaAiWatcher;

/// <summary>Reads named project-marker paths from the locally installed Codex bundle without modifying it.</summary>
public static class CodexProjectIconResolver
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Windows.Media.Geometry?> Cache =
        new(System.StringComparer.OrdinalIgnoreCase);
    private static readonly System.Lazy<System.Collections.Generic.Dictionary<string, AsarEntry>> AssetIndex =
        new(BuildAssetIndex);

    public static System.Windows.Media.Geometry? GetGeometry(string? iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return null;
        }

        return Cache.GetOrAdd(iconName.Trim(), ReadGeometry);
    }

    private static System.Windows.Media.Geometry? ReadGeometry(string iconName)
    {
        try
        {
            var index = AssetIndex.Value;
            var candidates = index
                .Where(item => item.Key.StartsWith(iconName + "-", System.StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Value)
                .OrderBy(entry => entry.Path.EndsWith(".js", System.StringComparison.OrdinalIgnoreCase) ? 0 : 1);
            foreach (var entry in candidates)
            {
                var script = ReadAsarText(entry);
                if (!script.Contains("createLucideIcon", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var data = string.Join(" ", System.Text.RegularExpressions.Regex.Matches(script, "d:`(?<path>[^`]+)`")
                    .Select(match => match.Groups["path"].Value));
                foreach (System.Text.RegularExpressions.Match element in System.Text.RegularExpressions.Regex.Matches(script, "`(?<type>circle|rect|line|polyline|polygon)`,\\{(?<attributes>[^}]*)\\}"))
                {
                    var attributes = element.Groups["attributes"].Value;
                    if (element.Groups["type"].Value == "circle" &&
                        TryReadNumber(attributes, "cx", out var x) && TryReadNumber(attributes, "cy", out var y) && TryReadNumber(attributes, "r", out var radius))
                    {
                        data += $" M {x - radius},{y} A {radius},{radius} 0 1 1 {x + radius},{y} A {radius},{radius} 0 1 1 {x - radius},{y}";
                    }
                    else if (element.Groups["type"].Value == "rect" &&
                             TryReadNumber(attributes, "x", out x) && TryReadNumber(attributes, "y", out y) &&
                             TryReadNumber(attributes, "width", out var width) && TryReadNumber(attributes, "height", out var height))
                    {
                        data += $" M {x},{y} H {x + width} V {y + height} H {x} Z";
                    }
                    else if (element.Groups["type"].Value == "line" &&
                             TryReadNumber(attributes, "x1", out var x1) && TryReadNumber(attributes, "y1", out var y1) &&
                             TryReadNumber(attributes, "x2", out var x2) && TryReadNumber(attributes, "y2", out var y2))
                    {
                        data += $" M {x1},{y1} L {x2},{y2}";
                    }
                    else if (element.Groups["type"].Value is "polyline" or "polygon" && TryReadText(attributes, "points", out var points))
                    {
                        data += " M " + points.Replace(" ", " L ", System.StringComparison.Ordinal);
                        if (element.Groups["type"].Value == "polygon") data += " Z";
                    }
                }

                return string.IsNullOrWhiteSpace(data) ? null : System.Windows.Media.Geometry.Parse(data);
            }
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException or System.Text.Json.JsonException or System.FormatException)
        {
        }

        return null;
    }

    private static System.Collections.Generic.Dictionary<string, AsarEntry> BuildAssetIndex()
    {
        var archive = FindCodexArchive();
        if (archive is null)
        {
            return new(System.StringComparer.OrdinalIgnoreCase);
        }

        using var stream = new System.IO.FileStream(archive, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
        using var reader = new System.IO.BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        _ = reader.ReadUInt32();
        var headerSize = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var headerLength = checked((int)headerSize - 8);
        using var document = System.Text.Json.JsonDocument.Parse(reader.ReadBytes(headerLength));
        var result = new System.Collections.Generic.Dictionary<string, AsarEntry>(System.StringComparer.OrdinalIgnoreCase);
        if (document.RootElement.TryGetProperty("files", out var files))
        {
            IndexFiles(files, string.Empty, stream.Position, result);
        }

        return result;
    }

    private static void IndexFiles(System.Text.Json.JsonElement files, string path, long contentOffset, System.Collections.Generic.Dictionary<string, AsarEntry> result)
    {
        foreach (var child in files.EnumerateObject())
        {
            var childPath = string.IsNullOrEmpty(path) ? child.Name : path + "/" + child.Name;
            if (child.Value.TryGetProperty("files", out var nested))
            {
                IndexFiles(nested, childPath, contentOffset, result);
                continue;
            }

            if (!childPath.StartsWith("webview/assets/", System.StringComparison.OrdinalIgnoreCase) ||
                !childPath.EndsWith(".js", System.StringComparison.OrdinalIgnoreCase) ||
                !child.Value.TryGetProperty("offset", out var offset) ||
                !child.Value.TryGetProperty("size", out var size))
            {
                continue;
            }

            result[child.Name] = new AsarEntry(childPath, contentOffset + long.Parse(offset.GetString()!, System.Globalization.CultureInfo.InvariantCulture), size.GetInt64());
        }
    }

    private static string ReadAsarText(AsarEntry entry)
    {
        var archive = FindCodexArchive() ?? throw new System.IO.FileNotFoundException();
        using var stream = new System.IO.FileStream(archive, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
        stream.Position = entry.Offset;
        var bytes = new byte[checked((int)entry.Size)];
        stream.ReadExactly(bytes);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static bool TryReadNumber(string attributes, string name, out double value) =>
        double.TryParse(ReadAttribute(attributes, name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);

    private static bool TryReadText(string attributes, string name, out string value)
    {
        value = ReadAttribute(attributes, name) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? ReadAttribute(string attributes, string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(attributes, $"(?:^|,){System.Text.RegularExpressions.Regex.Escape(name)}:`(?<value>[^`]+)`");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? FindCodexArchive()
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("codex"))
        {
            try
            {
                var executable = process.MainModule?.FileName;
                var archive = executable is null ? null : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(executable)!, "app.asar");
                if (archive is not null && System.IO.File.Exists(archive)) return archive;
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    private sealed record AsarEntry(string Path, long Offset, long Size);
}
