namespace VolturaAiWatcher.Tests;

public sealed class ScreenEdgeTuckPolicyTests
{
    [Fact]
    public void ExpandedPosition_RightAlignsAndVerticallyCentersWindow()
    {
        var position = ScreenEdgeTuckPolicy.GetExpandedPosition(
            new NativeBounds(1920, 40, 2560, 1400),
            windowWidth: 840,
            windowHeight: 1000);

        Assert.Equal(new NativePoint(3640, 240), position);
    }

    [Fact]
    public void TuckedPosition_LeavesOnlyScaledTabInsideNegativeOriginWorkArea()
    {
        var tabWidth = ScreenEdgeTuckPolicy.GetTabWidthPixels(1.5);
        var workArea = new NativeBounds(-2560, -120, 2560, 1440);

        var position = ScreenEdgeTuckPolicy.GetTuckedPosition(
            workArea,
            windowHeight: 975,
            tabWidth);

        Assert.Equal(42, tabWidth);
        Assert.Equal(new NativePoint(-42, 112), position);
        Assert.Equal(workArea.Right, position.X + tabWidth);
    }

    [Fact]
    public void MinimizedEquivalent_IncludesHiddenMinimizedAndTuckTransitions()
    {
        var cases = new[]
        {
            (IsVisible: false, IsMinimized: false, State: WindowTuckState.Expanded, Expected: true),
            (IsVisible: true, IsMinimized: true, State: WindowTuckState.Expanded, Expected: true),
            (IsVisible: true, IsMinimized: false, State: WindowTuckState.Tucking, Expected: true),
            (IsVisible: true, IsMinimized: false, State: WindowTuckState.Tucked, Expected: true),
            (IsVisible: true, IsMinimized: false, State: WindowTuckState.Restoring, Expected: true),
            (IsVisible: true, IsMinimized: false, State: WindowTuckState.Expanded, Expected: false)
        };

        foreach (var testCase in cases)
        {
            Assert.Equal(
                testCase.Expected,
                ScreenEdgeTuckPolicy.IsMinimizedEquivalent(
                    testCase.IsVisible,
                    testCase.IsMinimized,
                    testCase.State));
        }
    }

    [Fact]
    public void TransitionPolicy_IgnoresRepeatedRequestsDuringAnimations()
    {
        Assert.True(ScreenEdgeTuckPolicy.CanStartTuck(WindowTuckState.Expanded));
        Assert.False(ScreenEdgeTuckPolicy.CanStartTuck(WindowTuckState.Tucking));
        Assert.False(ScreenEdgeTuckPolicy.CanStartTuck(WindowTuckState.Restoring));
        Assert.False(ScreenEdgeTuckPolicy.CanStartRestore(WindowTuckState.Tucking));
        Assert.True(ScreenEdgeTuckPolicy.CanStartRestore(WindowTuckState.Tucked));
        Assert.False(ScreenEdgeTuckPolicy.CanStartRestore(WindowTuckState.Restoring));
    }

    [Fact]
    public void ResizePolicy_AllowsResizeOnlyWhileFullyExpanded()
    {
        Assert.True(ScreenEdgeTuckPolicy.ShouldAllowResize(WindowTuckState.Expanded));
        Assert.False(ScreenEdgeTuckPolicy.ShouldAllowResize(WindowTuckState.Tucking));
        Assert.False(ScreenEdgeTuckPolicy.ShouldAllowResize(WindowTuckState.Tucked));
        Assert.False(ScreenEdgeTuckPolicy.ShouldAllowResize(WindowTuckState.Restoring));
    }

    [Fact]
    public void SelectMonitor_PrefersCachedMonitorWhenStillAvailable()
    {
        var monitors = CreateMonitors();

        var selected = ScreenEdgeTuckPolicy.SelectMonitor(
            @"\\.\DISPLAY2",
            monitors,
            new NativeBounds(2200, 100, 420, 650));

        Assert.Equal(@"\\.\DISPLAY2", selected.DeviceName);
    }

    [Fact]
    public void SelectMonitor_UsesLargestWindowIntersectionWhenCachedMonitorWasRemoved()
    {
        var monitors = CreateMonitors();

        var selected = ScreenEdgeTuckPolicy.SelectMonitor(
            @"\\.\REMOVED",
            monitors,
            new NativeBounds(1800, 100, 500, 650));

        Assert.Equal(@"\\.\DISPLAY2", selected.DeviceName);
    }

    [Fact]
    public void SelectMonitor_FallsBackToPrimaryWhenWindowIntersectsNoMonitor()
    {
        var monitors = CreateMonitors();

        var selected = ScreenEdgeTuckPolicy.SelectMonitor(
            null,
            monitors,
            new NativeBounds(9000, 9000, 420, 650));

        Assert.True(selected.IsPrimary);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.25, 0.0625)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.75, 0.9375)]
    [InlineData(1, 1)]
    public void EaseInOutCubic_UsesExpectedSmoothCurve(double progress, double expected) =>
        Assert.Equal(expected, ScreenEdgeTuckPolicy.EaseInOutCubic(progress), precision: 6);

    private static MonitorWorkArea[] CreateMonitors() =>
        [
            new(@"\\.\DISPLAY1", new NativeBounds(0, 0, 1920, 1040), true),
            new(@"\\.\DISPLAY2", new NativeBounds(1920, -100, 2560, 1440), false)
        ];
}
