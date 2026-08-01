namespace VolturaAiWatcher.Tests;

public sealed class NotificationMessagePolicyTests
{
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
        Assert.True(NotificationMessagePolicy.ShouldShow(sender, onlyShowCodexResponses: false));
    }

    [Fact]
    public void CodexResponsesOnlySettingShowsCodexResponses()
    {
        Assert.True(NotificationMessagePolicy.ShouldShow("Codex", onlyShowCodexResponses: true));
    }

    [Fact]
    public void CodexResponsesOnlySettingHidesUserPrompts()
    {
        Assert.False(NotificationMessagePolicy.ShouldShow("You", onlyShowCodexResponses: true));
    }
}
