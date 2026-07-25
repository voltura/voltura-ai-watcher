namespace VolturaAiWatcher;

public sealed record CodexParsedRecord(
    string? ThreadId,
    string? WorkingDirectory,
    string? Sender,
    string? Message,
    CodexChatStatus? Status,
    System.DateTimeOffset OccurredAt);

public static class CodexRecordParser
{
    public static bool TryParse(string line, out CodexParsedRecord? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(line);
            var root = document.RootElement;
            var occurredAt = ReadTimestamp(root);
            var recordType = ReadString(root, "type");
            if (!root.TryGetProperty("payload", out var payload) ||
                payload.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return false;
            }

            if (string.Equals(recordType, "session_meta", System.StringComparison.Ordinal))
            {
                parsed = new CodexParsedRecord(
                    ReadString(payload, "session_id") ?? ReadString(payload, "id"),
                    ReadString(payload, "cwd"),
                    null,
                    null,
                    null,
                    occurredAt);
                return true;
            }

            if (!string.Equals(recordType, "event_msg", System.StringComparison.Ordinal))
            {
                return false;
            }

            var eventType = ReadString(payload, "type");
            switch (eventType)
            {
                case "user_message":
                    parsed = CreateMessage(payload, occurredAt, "You", CodexChatStatus.Starting);
                    return parsed.Message is not null;
                case "agent_message":
                    parsed = CreateMessage(payload, occurredAt, "Codex", CodexChatStatus.Working);
                    return parsed.Message is not null;
                case "task_started":
                case "turn_started":
                    parsed = CreateStatus(occurredAt, CodexChatStatus.Working);
                    return true;
                case "task_complete":
                case "turn_complete":
                    parsed = CreateStatus(occurredAt, CodexChatStatus.Completed);
                    return true;
                case "request_user_input":
                    parsed = CreateStatus(occurredAt, CodexChatStatus.WaitingForInput);
                    return true;
                case "exec_approval_request":
                case "apply_patch_approval_request":
                case "patch_approval_request":
                    parsed = CreateStatus(occurredAt, CodexChatStatus.WaitingForApproval);
                    return true;
                case "elicitation_request":
                case "mcp_elicitation_request":
                    parsed = CreateStatus(occurredAt, CodexChatStatus.WaitingForConnector);
                    return true;
                case "turn_aborted":
                case "task_aborted":
                case "interrupted":
                    parsed = CreateStatus(occurredAt, CodexChatStatus.Interrupted);
                    return true;
                case "error":
                case "stream_error":
                    parsed = CreateStatus(occurredAt, CodexChatStatus.Failed);
                    return true;
                default:
                    return false;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public static string CreateMessageId(
        string threadId,
        System.DateTimeOffset occurredAt,
        string sender,
        string message)
    {
        var raw = $"{threadId}\n{occurredAt:O}\n{sender}\n{message}";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return System.Convert.ToHexString(bytes);
    }

    private static CodexParsedRecord CreateMessage(
        System.Text.Json.JsonElement payload,
        System.DateTimeOffset occurredAt,
        string sender,
        CodexChatStatus status)
    {
        var message = ReadString(payload, "message");
        if (string.IsNullOrWhiteSpace(message))
        {
            message = ReadString(payload, "text");
        }

        return new CodexParsedRecord(
            null,
            null,
            sender,
            string.IsNullOrWhiteSpace(message) ? null : NormalizeMessage(message),
            status,
            occurredAt);
    }

    private static CodexParsedRecord CreateStatus(
        System.DateTimeOffset occurredAt,
        CodexChatStatus status) =>
        new(null, null, null, null, status, occurredAt);

    private static string NormalizeMessage(string message) =>
        message.Replace("\r\n", "\n", System.StringComparison.Ordinal).Trim();

    private static System.DateTimeOffset ReadTimestamp(System.Text.Json.JsonElement root)
    {
        var value = ReadString(root, "timestamp");
        return System.DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var timestamp)
            ? timestamp
            : System.DateTimeOffset.UtcNow;
    }

    private static string? ReadString(System.Text.Json.JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == System.Text.Json.JsonValueKind.String
            ? property.GetString()
            : null;
}
