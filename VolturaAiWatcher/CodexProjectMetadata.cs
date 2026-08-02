namespace VolturaAiWatcher;

public sealed record CodexProjectMetadata(string Name, string Color, string? Icon)
{
    public static readonly CodexProjectMetadata Fallback = new("Codex", "green", null);

    public string ColorHex => Color.ToLowerInvariant() switch
    {
        "gray" or "grey" => "#E8E8E8",
        "red" => "#FF5C61",
        "orange" => "#FF7F41",
        "yellow" => "#FFCF3E",
        "green" => "#3FCF77",
        "blue" => "#3497F4",
        "purple" => "#A16CF0",
        "pink" => "#F27BB5",
        _ => "#3FCF77"
    };

    public string? IconGeometry => Icon?.ToLowerInvariant() switch
    {
        "folder" => "M2,5 H7 L9,7 H16 V14 A2,2 0 0 1 14,16 H4 A2,2 0 0 1 2,14 Z",
        "currency-dollar" or "circle-dollar-sign" => "M16,8 H10 A2,2 0 1 0 10,12 H14 A2,2 0 1 1 14,16 H8 M12,18 V6 M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12",
        "book-open" => "M3,3 H9 A2,2 0 0 1 11,5 V15 A2,2 0 0 0 9,13 H3 Z M11,5 A2,2 0 0 1 13,3 H15 V13 H13 A2,2 0 0 0 11,15",
        "graduation-cap" => "M2,7 L9,3 L16,7 L9,11 Z M5,9 V12 C7,15 11,15 13,12 V9 M15,8 V12",
        "pencil" => "M3,13 L4,9 L11,2 A2,2 0 0 1 14,5 L7,12 Z M10,3 L13,6",
        "pen-tool" => "M3,14 L5,6 L11,3 L15,7 L10,14 Z M5,6 L10,14 M11,3 L10,14 M9,10 H10",
        "braces" => "M6,2 C4,2 4,4 4,5 V6 C4,7 3,8 2,8 C3,8 4,9 4,10 V11 C4,13 4,15 6,15 M12,2 C14,2 14,4 14,5 V6 C14,7 15,8 16,8 C15,8 14,9 14,10 V11 C14,13 14,15 12,15",
        "terminal" or "square-terminal" => "M3,3 H13 A2,2 0 0 1 15,5 V13 A2,2 0 0 1 13,15 H3 A2,2 0 0 1 1,13 V5 A2,2 0 0 1 3,3 Z M4,7 L6,9 L4,11 M8,11 H12",
        "music" => "M12,3 V12 M12,3 L16,2 V10 M12,6 L16,5 M8,13 A3,3 0 1 1 5,10 A3,3 0 0 1 8,13 M16,11 A3,3 0 1 1 13,8",
        "popcorn" => "M5,6 H13 L12,15 H6 Z M4,5 C4,3 6,3 7,5 C8,2 10,2 11,5 C12,3 14,3 14,5 M7,7 L8,14 M10,7 L9,14",
        "palette" => "M9,2 A7,7 0 1 0 9,16 C11,16 11,13 10,12 C9,10 11,9 13,10 C15,11 16,9 16,8 A6,6 0 0 0 9,2 M5,7 H5.1 M8,5 H8.1 M12,6 H12.1 M5,11 H5.1",
        "stethoscope" => "M11,2 V4 M5,2 V4 M5,3 H4 A2,2 0 0 0 2,5 V9 A6,6 0 0 0 14,9 V5 A2,2 0 0 0 12,3 H11 M8,15 A6,6 0 0 0 20,15 V12 M22,10 A2,2 0 1 1 18,10 A2,2 0 1 1 22,10",
        "asterisk" => "M9,2 V16 M3,5 L15,13 M15,5 L3,13",
        "flower" or "flower-2" => "M9,8 C5,3 2,6 5,9 C2,12 5,15 9,10 C13,15 16,12 13,9 C16,6 13,3 9,8 M9,2 V16 M2,9 H16",
        "tulip" => "M5,4 C7,4 7,7 9,7 C11,7 11,4 13,4 C14,9 12,12 9,12 C6,12 4,9 5,4 M9,12 V16 M5,16 H13",
        "briefcase" or "briefcase-business" => "M2,6 H16 V14 A2,2 0 0 1 14,16 H4 A2,2 0 0 1 2,14 Z M6,6 V4 A2,2 0 0 1 8,2 H10 A2,2 0 0 1 12,4 V6 M2,10 H16",
        "chart-no-axes-column" => "M3,15 V9 H6 V15 M7,15 V4 H10 V15 M11,15 V7 H14 V15",
        "orbit" => "M9,2 A7,7 0 1 0 9,16 A7,7 0 0 0 9,2 M3,5 C5,8 13,10 15,13",
        "dumbbell" => "M3,5 V13 M5,3 V15 M7,8 H11 M13,3 V15 M15,5 V13",
        "notebook-tabs" => "M4,2 H13 V16 H4 Z M4,5 H2 M4,9 H2 M4,13 H2 M7,6 H11 M7,10 H11",
        "scale" => "M9,2 V15 M4,5 H14 M5,5 L3,10 H7 Z M13,5 L11,10 H15 Z M5,15 H13",
        "globe-2" => "M9,2 A7,7 0 1 0 9,16 A7,7 0 0 0 9,2 M2,9 H16 M9,2 C6,6 6,12 9,16 M9,2 C12,6 12,12 9,16",
        "plane" => "M17.8,19.2 L16,11 L19.5,7.5 C21,6 21.5,4 21,3 C20,2.5 18,3 16.5,4.5 L13,8 L4.8,6.2 C4.3,6.1 3.9,6.3 3.7,6.7 L3.4,7.2 C3.2,7.7 3.3,8.2 3.7,8.5 L9,12 L7,15 H4 L3,16 L6,18 L8,21 L9,20 V17 L12,15 L15.5,20.3 C15.8,20.7 16.3,20.8 16.8,20.6 L17.3,20.4 C17.7,20.1 17.9,19.7 17.8,19.2 Z",
        "wrench" => "M14.7,6.3 A1,1 0 0 0 14.7,7.7 L16.3,9.3 A1,1 0 0 0 17.7,9.3 L21.47,5.53 A6,6 0 0 1 13.53,13.47 L6.62,20.38 A2.12,2.12 0 0 1 3.62,17.38 L10.53,10.47 A6,6 0 0 1 18.47,2.53 L14.7,6.3 Z",
        "paw-print" => "M6,8 A2,2 0 1 1 4,6 A2,2 0 0 1 6,8 M12,8 A2,2 0 1 1 10,6 A2,2 0 0 1 12,8 M4,4 A1,1 0 1 1 3,3 M14,4 A1,1 0 1 1 13,3 M9,15 C5,15 4,12 6,10 C7,9 8,10 9,11 C10,10 11,9 12,10 C14,12 13,15 9,15",
        "flask-conical" => "M6,2 H12 M8,2 V7 L4,14 A2,2 0 0 0 6,16 H12 A2,2 0 0 0 14,14 L10,7 V2 M6,11 H12",
        "brain" => "M9,3 C7,1 4,3 5,6 C2,6 2,10 5,11 C4,14 7,16 9,14 C11,16 14,14 13,11 C16,10 16,6 13,6 C14,3 11,1 9,3 M9,3 V14",
        "heart" => "M9,15 L3,9 C1,5 6,2 9,6 C12,2 17,5 15,9 Z",
        "gift" => "M3,7 H15 V15 H3 Z M2,7 H16 V10 H2 Z M9,7 V15 M6,7 C3,6 4,2 6,3 C8,4 9,7 9,7 M12,7 C15,6 14,2 12,3 C10,4 9,7 9,7",
        _ => null
    };

    public bool UsesOutlineIcon => IconGeometry is not null;
}
