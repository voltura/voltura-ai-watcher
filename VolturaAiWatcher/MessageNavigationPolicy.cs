namespace VolturaAiWatcher;

public static class MessageNavigationPolicy
{
    public static bool CanOpenNext(int currentIndex) => currentIndex > 0;

    public static bool CanOpenPrevious(int currentIndex, int entryCount) =>
        currentIndex < entryCount - 1;

    public static int GetNextIndex(int currentIndex) => currentIndex - 1;

    public static int GetPreviousIndex(int currentIndex) => currentIndex + 1;
}
