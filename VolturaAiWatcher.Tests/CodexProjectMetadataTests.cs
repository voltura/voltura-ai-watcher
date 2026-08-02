namespace VolturaAiWatcher.Tests;

public sealed class CodexProjectMetadataTests
{
    [Fact]
    public void StethoscopeUsesAStrokeGeometryRatherThanAFilledSilhouette()
    {
        var metadata = new CodexProjectMetadata("Download watcher", "green", "stethoscope");

        Assert.True(metadata.UsesOutlineIcon);
        Assert.Contains("A6,6", metadata.IconGeometry);
    }

    [Theory]
    [InlineData("plane")]
    [InlineData("wrench")]
    [InlineData("currency-dollar")]
    public void SupportedMarkersUseTheSharedOutlineTreatment(string icon)
    {
        var metadata = new CodexProjectMetadata("Project", "green", icon);

        Assert.True(metadata.UsesOutlineIcon);
        Assert.False(string.IsNullOrWhiteSpace(metadata.IconGeometry));
    }
}
