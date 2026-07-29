namespace VolturaAiWatcher;

internal sealed record JsonMessagePresentation(string PreviewText, string DetailText);

internal static class JsonMessageFormatter
{
    private static readonly System.Text.Json.JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };

    public static JsonMessagePresentation? TryFormat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
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

    private static JsonMessagePresentation FormatObject(System.Text.Json.JsonElement element)
    {
        var properties = element.EnumerateObject().ToArray();
        if (properties.Length == 0)
        {
            return new JsonMessagePresentation("EMPTY JSON OBJECT", "{}");
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
        return new JsonMessagePresentation(preview, detail);
    }

    private static JsonMessagePresentation FormatArray(System.Text.Json.JsonElement element)
    {
        var itemCount = element.GetArrayLength();
        var label = itemCount == 1 ? "1 JSON ITEM" : $"{itemCount} JSON ITEMS";
        return new JsonMessagePresentation(
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
        var value = property.Value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => property.Value.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array =>
                System.Text.Json.JsonSerializer.Serialize(property.Value, IndentedJsonOptions),
            System.Text.Json.JsonValueKind.Null => "null",
            _ => property.Value.GetRawText()
        };
        return IsDecisionMetric(property.Name) ? value.ToUpperInvariant() : value;
    }

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
