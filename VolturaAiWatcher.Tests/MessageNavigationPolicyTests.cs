namespace VolturaAiWatcher.Tests;

public sealed class MessageNavigationPolicyTests
{
    [Fact]
    public void Next_MovesTowardNewerMessageAtTopOfDescendingList()
    {
        Assert.True(MessageNavigationPolicy.CanOpenNext(currentIndex: 2));
        Assert.Equal(1, MessageNavigationPolicy.GetNextIndex(currentIndex: 2));
    }

    [Fact]
    public void Previous_MovesTowardOlderMessageBelowCurrentMessage()
    {
        Assert.True(MessageNavigationPolicy.CanOpenPrevious(currentIndex: 1, entryCount: 3));
        Assert.Equal(2, MessageNavigationPolicy.GetPreviousIndex(currentIndex: 1));
    }

    [Fact]
    public void NavigationStopsAtBothEnds()
    {
        Assert.False(MessageNavigationPolicy.CanOpenNext(currentIndex: 0));
        Assert.False(MessageNavigationPolicy.CanOpenPrevious(currentIndex: 2, entryCount: 3));
    }
}
