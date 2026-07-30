namespace VolturaAiWatcher;

internal enum WindowTuckState
{
    Expanded,
    Tucking,
    Tucked,
    Restoring
}

internal readonly record struct NativePoint(int X, int Y);

internal readonly record struct NativeBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

internal readonly record struct MonitorWorkArea(string DeviceName, NativeBounds Bounds, bool IsPrimary);

internal static class ScreenEdgeTuckPolicy
{
    public const double TabWidthDip = 28;
    public const int AnimationDurationMilliseconds = 225;

    public static int GetTabWidthPixels(double dpiScale) =>
        System.Math.Max(1, (int)System.Math.Round(TabWidthDip * dpiScale));

    public static NativePoint GetExpandedPosition(
        NativeBounds workArea,
        int windowWidth,
        int windowHeight) =>
        new(
            workArea.Right - windowWidth,
            workArea.Top + (workArea.Height - windowHeight) / 2);

    public static NativePoint GetTuckedPosition(
        NativeBounds workArea,
        int windowHeight,
        int tabWidthPixels) =>
        new(
            workArea.Right - tabWidthPixels,
            workArea.Top + (workArea.Height - windowHeight) / 2);

    public static bool IsMinimizedEquivalent(
        bool isVisible,
        bool isMinimized,
        WindowTuckState tuckState) =>
        !isVisible ||
        isMinimized ||
        tuckState is WindowTuckState.Tucking or WindowTuckState.Tucked or WindowTuckState.Restoring;

    public static bool CanStartTuck(WindowTuckState tuckState) =>
        tuckState == WindowTuckState.Expanded;

    public static bool CanStartRestore(WindowTuckState tuckState) =>
        tuckState == WindowTuckState.Tucked;

    public static bool ShouldAllowResize(WindowTuckState tuckState) =>
        tuckState == WindowTuckState.Expanded;

    public static MonitorWorkArea SelectMonitor(
        string? preferredDeviceName,
        System.Collections.Generic.IReadOnlyList<MonitorWorkArea> monitors,
        NativeBounds windowBounds)
    {
        if (monitors.Count == 0)
        {
            throw new System.ArgumentException("At least one monitor is required.", nameof(monitors));
        }

        if (!string.IsNullOrWhiteSpace(preferredDeviceName))
        {
            foreach (var monitor in monitors)
            {
                if (string.Equals(
                    monitor.DeviceName,
                    preferredDeviceName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    return monitor;
                }
            }
        }

        var best = monitors[0];
        var bestArea = IntersectionArea(best.Bounds, windowBounds);
        foreach (var monitor in monitors.Skip(1))
        {
            var area = IntersectionArea(monitor.Bounds, windowBounds);
            if (area > bestArea || (area == bestArea && monitor.IsPrimary && !best.IsPrimary))
            {
                best = monitor;
                bestArea = area;
            }
        }

        if (bestArea > 0)
        {
            return best;
        }

        return monitors.FirstOrDefault(monitor => monitor.IsPrimary, monitors[0]);
    }

    public static double EaseInOutCubic(double progress)
    {
        var value = System.Math.Clamp(progress, 0, 1);
        return value < 0.5
            ? 4 * value * value * value
            : 1 - System.Math.Pow(-2 * value + 2, 3) / 2;
    }

    private static long IntersectionArea(NativeBounds first, NativeBounds second)
    {
        var width = System.Math.Max(0, System.Math.Min(first.Right, second.Right) - System.Math.Max(first.Left, second.Left));
        var height = System.Math.Max(0, System.Math.Min(first.Bottom, second.Bottom) - System.Math.Max(first.Top, second.Top));
        return (long)width * height;
    }
}
