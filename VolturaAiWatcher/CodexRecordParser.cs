namespace VolturaAiWatcher;

public sealed record CodexParsedRecord(
    string? ThreadId,
    string? WorkingDirectory,
    string? Sender,
    string? Message,
    CodexChatStatus? Status,
    System.DateTimeOffset OccurredAt,
    string? Model = null,
    CodexUsageSnapshot? Usage = null);

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

            if (string.Equals(recordType, "turn_context", System.StringComparison.Ordinal))
            {
                var model = ReadString(payload, "model");
                if (string.IsNullOrWhiteSpace(model))
                {
                    return false;
                }

                parsed = new CodexParsedRecord(
                    null,
                    null,
                    null,
                    null,
                    null,
                    occurredAt,
                    model.Trim());
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
                case "token_count":
                    return TryCreateUsage(payload, occurredAt, out parsed);
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

    private static bool TryCreateUsage(
        System.Text.Json.JsonElement payload,
        System.DateTimeOffset occurredAt,
        out CodexParsedRecord? parsed)
    {
        parsed = null;
        if (!TryReadObject(payload, "info", out var info))
        {
            return false;
        }

        var contextTokensUsed = TryReadObject(info, "last_token_usage", out var lastTokenUsage)
            ? ReadInt64(lastTokenUsage, "total_tokens")
            : null;
        var contextWindowTokens = ReadInt64(info, "model_context_window");
        var weeklyRemainingPercent = default(double?);
        System.DateTimeOffset? weeklyResetAt = null;

        if (TryReadObject(payload, "rate_limits", out var rateLimits) &&
            TryReadObject(rateLimits, "primary", out var primary) &&
            ReadInt64(primary, "window_minutes") == CodexUsagePolicy.WeeklyWindowMinutes)
        {
            var usedPercent = ReadDouble(primary, "used_percent");
            if (usedPercent is { } used)
            {
                weeklyRemainingPercent = 100d - used;
            }

            if (ReadInt64(primary, "resets_at") is { } resetSeconds)
            {
                try
                {
                    weeklyResetAt = System.DateTimeOffset.FromUnixTimeSeconds(resetSeconds);
                }
                catch (System.ArgumentOutOfRangeException)
                {
                }
            }
        }

        if (contextTokensUsed is null &&
            contextWindowTokens is null &&
            weeklyRemainingPercent is null)
        {
            return false;
        }

        parsed = new CodexParsedRecord(
            null,
            null,
            null,
            null,
            null,
            occurredAt,
            Usage: new CodexUsageSnapshot(
                null,
                contextTokensUsed,
                contextWindowTokens,
                weeklyRemainingPercent,
                weeklyResetAt,
                occurredAt));
        return true;
    }

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

    private static long? ReadInt64(System.Text.Json.JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == System.Text.Json.JsonValueKind.Number &&
        property.TryGetInt64(out var value)
            ? value
            : null;

    private static double? ReadDouble(System.Text.Json.JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == System.Text.Json.JsonValueKind.Number &&
        property.TryGetDouble(out var value)
            ? value
            : null;

    private static bool TryReadObject(
        System.Text.Json.JsonElement element,
        string propertyName,
        out System.Text.Json.JsonElement value) =>
        element.TryGetProperty(propertyName, out value) &&
        value.ValueKind == System.Text.Json.JsonValueKind.Object;
}
