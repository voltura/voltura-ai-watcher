namespace VolturaAiWatcher.Tests;

public sealed class CodexChatStatusPolicyTests
{
    [Theory]
    [InlineData(CodexChatStatus.Starting)]
    [InlineData(CodexChatStatus.Working)]
    [InlineData(CodexChatStatus.WaitingForInput)]
    [InlineData(CodexChatStatus.WaitingForApproval)]
    [InlineData(CodexChatStatus.WaitingForConnector)]
    [InlineData(CodexChatStatus.Interrupted)]
    [InlineData(CodexChatStatus.Failed)]
    [InlineData(CodexChatStatus.Unknown)]
    public void AttentionAndActiveStatesAreRetained(CodexChatStatus status)
    {
        Assert.True(CodexChatStatusPolicy.RequiresRetention(status));
    }

    [Theory]
    [InlineData(CodexChatStatus.Idle)]
    [InlineData(CodexChatStatus.Completed)]
    [InlineData(CodexChatStatus.Archived)]
    public void ResolvedStatesCanBeCleared(CodexChatStatus status)
    {
        Assert.False(CodexChatStatusPolicy.RequiresRetention(status));
    }

    [Theory]
    [InlineData(CodexChatStatus.WaitingForInput)]
    [InlineData(CodexChatStatus.WaitingForApproval)]
    [InlineData(CodexChatStatus.WaitingForConnector)]
    public void WaitingStatesAreActionable(CodexChatStatus status)
    {
        Assert.True(CodexChatStatusPolicy.IsActionable(status));
    }

    [Theory]
    [InlineData(CodexChatStatus.Idle)]
    [InlineData(CodexChatStatus.Starting)]
    [InlineData(CodexChatStatus.Working)]
    [InlineData(CodexChatStatus.Completed)]
    [InlineData(CodexChatStatus.Interrupted)]
    [InlineData(CodexChatStatus.Failed)]
    [InlineData(CodexChatStatus.Archived)]
    [InlineData(CodexChatStatus.Unknown)]
    public void NonWaitingStatesAreNotActionable(CodexChatStatus status)
    {
        Assert.False(CodexChatStatusPolicy.IsActionable(status));
    }
}
