namespace VolturaAiWatcher;

public sealed record CodexProjectMetadata(string Name, string Color, string? Icon)
{
    public static readonly CodexProjectMetadata Fallback = new("Codex", "green", null);

    public string ColorHex => Color.ToLowerInvariant() switch
    {
        "blue" => "#3B9EFF", "green" => "#43D98A", "purple" => "#B38CFF",
        "orange" => "#FF9F43", "red" => "#FF647C", "yellow" => "#F6D365", _ => "#74D98A"
    };

    public string? IconGeometry => Icon?.ToLowerInvariant() switch
    {
        "plane" => "M2,9 L14,3 L11,14 L8,10 L4,12 L6,9 Z",
        "wrench" => "M12,3 A4,4 0 0 0 8,8 L3,13 L5,15 L10,10 A4,4 0 0 0 15,6 L12,8 L10,6 Z",
        "stethoscope" => "M4,3 V8 A3,3 0 0 0 10,8 V3 M4,6 H10 M10,9 A4,4 0 0 0 14,13 V14 A2,2 0 1 1 12,12",
        "currency-dollar" => "M9,2 V16 M12,5 C11,3 6,3 6,6 C6,9 12,7 12,11 C12,14 7,14 6,12",
        _ => null
    };
}
