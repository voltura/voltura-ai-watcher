namespace VolturaAiWatcher.Tests;

public sealed class JsonMessageFormatterTests
{
    [Fact]
    public void FormatsFullDecisionForScanningAndDetail()
    {
        const string message =
            """{"risk_level":"low","user_authorization":"high","outcome":"allow","rationale":"Routine and reversible."}""";

        var result = JsonMessageFormatter.TryFormat(message);

        Assert.NotNull(result);
        Assert.Equal(
            "ALLOW · RISK LEVEL: LOW · USER AUTHORIZATION: HIGH\nRoutine and reversible.",
            result.PreviewText);
        Assert.Equal(
            "OUTCOME // ALLOW\n\nRISK LEVEL // LOW\n\nUSER AUTHORIZATION // HIGH\n\nRATIONALE // Routine and reversible.",
            result.DetailText);
    }

    [Fact]
    public void FormatsOutcomeOnlyDecision()
    {
        var result = JsonMessageFormatter.TryFormat("""{"outcome":"allow"}""");

        Assert.NotNull(result);
        Assert.Equal("ALLOW", result.PreviewText);
        Assert.Equal("OUTCOME // ALLOW", result.DetailText);
    }

    [Fact]
    public void FormatsAnyObjectWithoutDecisionSpecificFields()
    {
        var result = JsonMessageFormatter.TryFormat(
            """{"taskName":"Build","attempt":2,"successful":true,"details":{"target":"win-x64"}}""");

        Assert.NotNull(result);
        Assert.Equal(
            "TASK NAME: Build · ATTEMPT: 2 · SUCCESSFUL: true · DETAILS: {…}",
            result.PreviewText);
        Assert.Contains("DETAILS // {", result.DetailText);
        Assert.Contains("\"target\": \"win-x64\"", result.DetailText);
    }

    [Fact]
    public void FormatsJsonArraysWithItemCountAndIndentedDetail()
    {
        var result = JsonMessageFormatter.TryFormat("""[{"id":1},{"id":2}]""");

        Assert.NotNull(result);
        Assert.Equal("2 JSON ITEMS", result.PreviewText);
        Assert.Contains("\n", result.DetailText);
    }

    [Theory]
    [InlineData("Ordinary assistant message")]
    [InlineData("{not valid json}")]
    [InlineData("\"a JSON string\"")]
    [InlineData("42")]
    public void LeavesNonStructuredMessagesUnformatted(string message)
    {
        Assert.Null(JsonMessageFormatter.TryFormat(message));
    }

    [Fact]
    public void EntryPreservesRawJsonForCopying()
    {
        const string raw = """{"outcome":"allow"}""";
        var entry = new CodexMessageEntry
        {
            Id = "1",
            ThreadId = "thread",
            ProjectName = "project",
            Sender = "Codex",
            Text = raw,
            OccurredAt = System.DateTimeOffset.UtcNow
        };

        Assert.Equal(raw, entry.Text);
        Assert.Equal("ALLOW", entry.PreviewText);
        Assert.Equal("OUTCOME // ALLOW", entry.DisplayText);
        Assert.Equal("STRUCTURED CODEX MESSAGE", entry.DetailHeading);
    }
}
