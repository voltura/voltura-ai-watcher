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
}
