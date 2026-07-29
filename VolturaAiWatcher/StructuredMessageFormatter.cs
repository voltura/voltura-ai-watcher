namespace VolturaAiWatcher;

internal sealed record StructuredMessagePresentation(string PreviewText, string DetailText);

internal static class StructuredMessageFormatter
{
    private const string TranscriptStartMarker = ">>> TRANSCRIPT DELTA START";
    private const string TranscriptEndMarker = ">>> TRANSCRIPT DELTA END";
    private const string ApprovalStartMarker = ">>> APPROVAL REQUEST START";
    private const string ApprovalEndMarker = ">>> APPROVAL REQUEST END";
    private const string PlannedActionMarker = "Planned action JSON:";

    private static readonly System.Text.Json.JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };

    public static StructuredMessagePresentation? TryFormat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var approvalReview = TryFormatApprovalReview(text);
        if (approvalReview is not null)
        {
            return approvalReview;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(text);
            return document.RootElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Object => FormatObject(document.RootElement),
                System.Text.Json.JsonValueKind.Array => FormatArray(document.RootElement),
                _ => null
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static StructuredMessagePresentation? TryFormatApprovalReview(string text)
    {
        var approvalStart = text.IndexOf(ApprovalStartMarker, System.StringComparison.Ordinal);
        var plannedActionStart = text.IndexOf(PlannedActionMarker, System.StringComparison.Ordinal);
        if (approvalStart < 0 || plannedActionStart < approvalStart)
        {
            return null;
        }

        var approvalEnd = text.IndexOf(
            ApprovalEndMarker,
            plannedActionStart + PlannedActionMarker.Length,
            System.StringComparison.Ordinal);
        if (approvalEnd < 0)
        {
            return null;
        }

        var plannedActionJson = text[
            (plannedActionStart + PlannedActionMarker.Length)..approvalEnd].Trim();
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(plannedActionJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return null;
            }

            return FormatApprovalReview(text, approvalStart, document.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static StructuredMessagePresentation FormatApprovalReview(
        string text,
        int approvalStart,
        System.Text.Json.JsonElement plannedAction)
    {
        var properties = plannedAction.EnumerateObject().ToArray();
        var tool = ReadStringProperty(properties, "tool");
        var justification = ReadStringProperty(properties, "justification");
        var access = ReadStringProperty(properties, "sandbox_permissions");
        var sessionId = ReadReviewedSessionId(text);
        var transcript = ReadMarkedSection(text, TranscriptStartMarker, TranscriptEndMarker);
        var formattedTranscript = string.IsNullOrWhiteSpace(transcript)
            ? null
            : FormatTranscript(transcript);

        var previewParts = new System.Collections.Generic.List<string> { "APPROVAL REQUEST" };
        if (!string.IsNullOrWhiteSpace(tool))
        {
            previewParts.Add(HumanizeName(tool));
        }

        if (!string.IsNullOrWhiteSpace(access))
        {
            previewParts.Add(FormatAccess(access));
        }

        var preview = string.Join(" · ", previewParts);
        if (!string.IsNullOrWhiteSpace(justification))
        {
            preview += $"\n{justification}";
        }

        var detailSections = new System.Collections.Generic.List<string> { "APPROVAL REQUEST" };
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            detailSections.Add($"SESSION // {sessionId}");
        }

        foreach (var property in OrderApprovalProperties(properties))
        {
            var label = GetApprovalPropertyLabel(property.Name);
            var value = property.Name.Equals(
                "sandbox_permissions",
                System.StringComparison.OrdinalIgnoreCase)
                ? FormatAccess(FormatCompactValue(property.Value))
                : FormatApprovalPropertyValue(property);
            detailSections.Add($"{label} // {value}");
        }

        var context = text[..approvalStart];
        if (!string.IsNullOrWhiteSpace(transcript))
        {
            var transcriptStart = context.IndexOf(TranscriptStartMarker, System.StringComparison.Ordinal);
            if (transcriptStart >= 0)
            {
                context = context[..transcriptStart];
            }
        }

        context = RemoveReviewedSessionLine(context).Trim();
        if (!string.IsNullOrWhiteSpace(context))
        {
            detailSections.Add($"CONTEXT //\n{context}");
        }

        if (!string.IsNullOrWhiteSpace(formattedTranscript))
        {
            detailSections.Add(formattedTranscript);
        }

        return new StructuredMessagePresentation(preview, string.Join("\n\n", detailSections));
    }

    private static string GetApprovalPropertyLabel(string name)
    {
        if (name.Equals("sandbox_permissions", System.StringComparison.OrdinalIgnoreCase))
        {
            return "ACCESS";
        }

        if (name.Equals("cwd", System.StringComparison.OrdinalIgnoreCase))
        {
            return "WORKING DIRECTORY";
        }

        return HumanizeName(name);
    }

    private static System.Collections.Generic.IEnumerable<System.Text.Json.JsonProperty> OrderApprovalProperties(
        System.Text.Json.JsonProperty[] properties)
    {
        string[] preferredOrder =
        [
            "tool",
            "sandbox_permissions",
            "cwd",
            "justification",
            "command"
        ];

        foreach (var name in preferredOrder)
        {
            foreach (var property in properties.Where(property =>
                         property.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
            {
                yield return property;
            }
        }

        foreach (var property in properties.Where(property =>
                     !preferredOrder.Contains(property.Name, System.StringComparer.OrdinalIgnoreCase)))
        {
            yield return property;
        }
    }

    private static string FormatApprovalPropertyValue(System.Text.Json.JsonProperty property)
    {
        if (property.Name.Equals("tool", System.StringComparison.OrdinalIgnoreCase) &&
            property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return HumanizeName(property.Value.GetString() ?? string.Empty);
        }

        if (property.Name.Equals("command", System.StringComparison.OrdinalIgnoreCase) &&
            property.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return string.Join(
                "\n",
                property.Value.EnumerateArray().Select(item =>
                    item.ValueKind == System.Text.Json.JsonValueKind.String
                        ? item.GetString() ?? string.Empty
                        : FormatDetailElement(item)));
        }

        return FormatDetailValue(property);
    }

    private static string FormatTranscript(string transcript)
    {
        var eventPattern = new System.Text.RegularExpressions.Regex(
            @"(?m)^\[(?<id>\d+)\]\s+(?<kind>[^:\r\n]+):\s?",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var matches = eventPattern.Matches(transcript);
        if (matches.Count == 0)
        {
            return $"TRANSCRIPT DELTA //\n{transcript.Trim()}";
        }

        var events = new System.Collections.Generic.List<string>();
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var bodyStart = match.Index + match.Length;
            var bodyEnd = index + 1 < matches.Count ? matches[index + 1].Index : transcript.Length;
            var body = transcript[bodyStart..bodyEnd].Trim();
            var formattedBody = FormatTranscriptBody(body);
            var header =
                $"[{match.Groups["id"].Value}] {HumanizeName(match.Groups["kind"].Value.Trim())}";
            events.Add(string.IsNullOrWhiteSpace(formattedBody)
                ? header
                : $"{header}\n{formattedBody}");
        }

        var eventLabel = matches.Count == 1 ? "1 EVENT" : $"{matches.Count} EVENTS";
        return $"TRANSCRIPT DELTA // {eventLabel}\n\n{string.Join("\n\n", events)}";
    }

    private static string FormatTranscriptBody(string body)
    {
        var wholeJson = TryFormatEmbeddedJson(body);
        if (wholeJson is not null)
        {
            return wholeJson;
        }

        for (var index = 0; index < body.Length - 1; index++)
        {
            if (body[index] != '\n' || body[index + 1] is not ('{' or '['))
            {
                continue;
            }

            var formattedJson = TryFormatEmbeddedJson(body[(index + 1)..].Trim());
            if (formattedJson is not null)
            {
                return $"{body[..index].TrimEnd()}\n\n{formattedJson}";
            }
        }

        return body;
    }

    private static string? TryFormatEmbeddedJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text[0] is not ('{' or '['))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(text);
            return document.RootElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Object => FormatObject(document.RootElement).DetailText,
                System.Text.Json.JsonValueKind.Array =>
                    System.Text.Json.JsonSerializer.Serialize(document.RootElement, IndentedJsonOptions),
                _ => null
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? ReadMarkedSection(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, System.StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += startMarker.Length;
        var end = text.IndexOf(endMarker, start, System.StringComparison.Ordinal);
        return end < 0 ? null : text[start..end].Trim();
    }

    private static string? ReadReviewedSessionId(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?m)^Reviewed Codex session id:\s*(?<id>[^\r\n]+)\s*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["id"].Value.Trim() : null;
    }

    private static string RemoveReviewedSessionLine(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(?m)^Reviewed Codex session id:\s*[^\r\n]+\s*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string? ReadStringProperty(
        System.Text.Json.JsonProperty[] properties,
        string name)
    {
        foreach (var property in properties)
        {
            if (property.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static string FormatAccess(string access) =>
        access.ToLowerInvariant() switch
        {
            "require_escalated" => "ELEVATED ACCESS",
            "use_default" => "STANDARD ACCESS",
            _ => HumanizeName(access)
        };

    private static StructuredMessagePresentation FormatObject(System.Text.Json.JsonElement element)
    {
        var properties = element.EnumerateObject().ToArray();
        if (properties.Length == 0)
        {
            return new StructuredMessagePresentation("EMPTY JSON OBJECT", "{}");
        }

        var hasOutcome = properties.Any(property =>
            string.Equals(property.Name, "outcome", System.StringComparison.OrdinalIgnoreCase));
        var orderedProperties = !hasOutcome
            ? properties
            : [
                properties.First(property =>
                    string.Equals(property.Name, "outcome", System.StringComparison.OrdinalIgnoreCase)),
                .. properties.Where(property =>
                    !string.Equals(property.Name, "outcome", System.StringComparison.OrdinalIgnoreCase))
            ];

        var previewParts = new System.Collections.Generic.List<string>();
        string? narrative = null;
        foreach (var property in orderedProperties)
        {
            var label = HumanizeName(property.Name);
            var value = FormatPreviewValue(property);
            if (IsNarrativeProperty(property.Name) &&
                property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                narrative ??= value;
                continue;
            }

            if (string.Equals(property.Name, "outcome", System.StringComparison.OrdinalIgnoreCase))
            {
                previewParts.Add(value);
            }
            else
            {
                previewParts.Add($"{label}: {value}");
            }
        }

        if (previewParts.Count == 0)
        {
            previewParts.Add("JSON OBJECT");
        }

        var preview = string.Join(" · ", previewParts);
        if (!string.IsNullOrWhiteSpace(narrative))
        {
            preview += $"\n{narrative}";
        }

        var detail = string.Join(
            "\n\n",
            orderedProperties.Select(property =>
                $"{HumanizeName(property.Name)} // {FormatDetailValue(property)}"));
        return new StructuredMessagePresentation(preview, detail);
    }

    private static StructuredMessagePresentation FormatArray(System.Text.Json.JsonElement element)
    {
        var itemCount = element.GetArrayLength();
        var label = itemCount == 1 ? "1 JSON ITEM" : $"{itemCount} JSON ITEMS";
        return new StructuredMessagePresentation(
            label,
            System.Text.Json.JsonSerializer.Serialize(element, IndentedJsonOptions));
    }

    private static string FormatCompactValue(System.Text.Json.JsonElement value) =>
        value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => value.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Object => "{…}",
            System.Text.Json.JsonValueKind.Array => $"[{value.GetArrayLength()} items]",
            System.Text.Json.JsonValueKind.Null => "null",
            _ => value.GetRawText()
        };

    private static string FormatPreviewValue(System.Text.Json.JsonProperty property)
    {
        var value = FormatCompactValue(property.Value);
        return IsDecisionMetric(property.Name) ? value.ToUpperInvariant() : value;
    }

    private static string FormatDetailValue(System.Text.Json.JsonProperty property)
    {
        var value = FormatDetailElement(property.Value);
        return IsDecisionMetric(property.Name) ? value.ToUpperInvariant() : value;
    }

    private static string FormatDetailElement(System.Text.Json.JsonElement value) =>
        value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => value.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array =>
                System.Text.Json.JsonSerializer.Serialize(value, IndentedJsonOptions),
            System.Text.Json.JsonValueKind.Null => "null",
            _ => value.GetRawText()
        };

    private static bool IsDecisionMetric(string name) =>
        name.Equals("outcome", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("risk_level", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("riskLevel", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("user_authorization", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("userAuthorization", System.StringComparison.OrdinalIgnoreCase);

    private static bool IsNarrativeProperty(string name) =>
        name.Equals("rationale", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("reason", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("description", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("message", System.StringComparison.OrdinalIgnoreCase) ||
        name.Equals("text", System.StringComparison.OrdinalIgnoreCase);

    private static string HumanizeName(string name)
    {
        var result = new System.Text.StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (character is '_' or '-')
            {
                if (result.Length > 0 && result[^1] != ' ')
                {
                    result.Append(' ');
                }

                continue;
            }

            if (index > 0 &&
                char.IsUpper(character) &&
                char.IsLower(name[index - 1]) &&
                result[^1] != ' ')
            {
                result.Append(' ');
            }

            result.Append(char.ToUpperInvariant(character));
        }

        return result.ToString().Trim();
    }
}
