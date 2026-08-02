namespace VolturaAiWatcher.Tests;

public sealed class NotificationMessagePolicyTests
{
    [Fact]
    public void NewSettingsShowMessageDetailsWhenMinimizedMessageIsClicked()
    {
        Assert.Equal(
            MinimizedMessageClickAction.ShowMessageDetails,
            new AppSettings().MinimizedMessageClickAction);
    }

    [Theory]
    [InlineData(MinimizedMessageClickAction.ShowMessageDetails)]
    [InlineData(MinimizedMessageClickAction.OpenInCodex)]
    public void MinimizedMessageClickActionPolicyPreservesSupportedValues(
        MinimizedMessageClickAction action)
    {
        Assert.Equal(action, MinimizedMessageClickActionPolicy.NormalizePersisted(action));
    }

    [Fact]
    public void MinimizedMessageClickActionPolicyFallsBackForUnknownValues()
    {
        Assert.Equal(
            MinimizedMessageClickAction.ShowMessageDetails,
            MinimizedMessageClickActionPolicy.NormalizePersisted((MinimizedMessageClickAction)99));
    }

    [Fact]
    public void NewSettingsKeepAllMessageNotificationsEnabledByDefault()
    {
        Assert.False(new AppSettings().OnlyShowCodexResponseNotifications);
    }

    [Theory]
    [InlineData("Codex")]
    [InlineData("You")]
    public void AllMessagesSettingShowsEverySender(string sender)
    {
        Assert.True(NotificationMessagePolicy.ShouldShow(
            monitoringPaused: false,
            sender,
            onlyShowCodexResponses: false));
    }

    [Fact]
    public void CodexResponsesOnlySettingShowsCodexResponses()
    {
        Assert.True(NotificationMessagePolicy.ShouldShow(
            monitoringPaused: false,
            "Codex",
            onlyShowCodexResponses: true));
    }

    [Fact]
    public void CodexResponsesOnlySettingHidesUserPrompts()
    {
        Assert.False(NotificationMessagePolicy.ShouldShow(
            monitoringPaused: false,
            "You",
            onlyShowCodexResponses: true));
    }

    [Theory]
    [InlineData("Codex", false)]
    [InlineData("Codex", true)]
    [InlineData("You", false)]
    public void PausedMonitoringHidesMinimizedNotifications(
        string sender,
        bool onlyShowCodexResponses)
    {
        Assert.False(NotificationMessagePolicy.ShouldShow(
            monitoringPaused: true,
            sender,
            onlyShowCodexResponses));
    }
}
