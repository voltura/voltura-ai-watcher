namespace VolturaAiWatcher.Tests;

public sealed class CodexWindowActivatorTests
{
    [Fact]
    public void MinimizedWindowRequiresRestore()
    {
        Assert.True(CodexWindowActivator.ShouldRestoreWindow(isIconic: true));
    }

    [Fact]
    public void VisibleWindowKeepsItsCurrentPlacement()
    {
        Assert.False(CodexWindowActivator.ShouldRestoreWindow(isIconic: false));
    }

    [Fact]
    public void BuildsRepositoryScopedNewChatReviewUri()
    {
        var uri = CodexWindowActivator.BuildNewChatUri(
            @"C:\source\Repo With Space\Räven",
            "/review");

        Assert.StartsWith("codex://new?path=", uri);
        Assert.Contains("Repo%20With%20Space", uri);
        Assert.Contains("R%C3%A4ven", uri);
        Assert.EndsWith("&prompt=%2Freview", uri);
    }

    [Fact]
    public void NewChatUriNormalizesTheWorkspacePath()
    {
        var uri = CodexWindowActivator.BuildNewChatUri(
            @"C:\source\repo\nested\..",
            "/review");

        Assert.Contains("C%3A%5Csource%5Crepo", uri);
        Assert.DoesNotContain("nested", uri);
    }
}
