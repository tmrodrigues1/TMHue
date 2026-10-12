using TMHue.Core.Models;
using TMHue.Windows.Display;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class WorkAreaPlacementTests
{
    private static readonly PixelBounds[] TwoMonitors =
    {
        new(0, 0, 1920, 1040),
        new(1920, 0, 2560, 1400)
    };

    [Theory]
    [InlineData(804, 212)]      // well inside the primary
    [InlineData(3000, 600)]     // on the secondary
    [InlineData(1800, 300)]     // straddling both
    [InlineData(-300, 100)]     // mostly off the left edge, title still grabbable
    public void TitleAreaInsideAWorkArea_IsReachable(int left, int top) =>
        Assert.True(WorkAreaPlacement.IsReachable(new(left, top, 440, 56), TwoMonitors));

    [Theory]
    [InlineData(5000, 300)]     // monitor that is no longer attached
    [InlineData(-2000, 300)]
    [InlineData(804, 1500)]     // below every work area
    [InlineData(804, -200)]     // title bar above the top edge
    [InlineData(-420, 100)]     // only a sliver left on screen
    public void TitleAreaOutsideEveryWorkArea_IsNotReachable(int left, int top) =>
        Assert.False(WorkAreaPlacement.IsReachable(new(left, top, 440, 56), TwoMonitors));

    [Fact]
    public void RemovingTheMonitorThatHeldTheWindow_MakesItUnreachable()
    {
        var titleArea = new PixelBounds(3000, 600, 440, 56);

        Assert.True(WorkAreaPlacement.IsReachable(titleArea, TwoMonitors));
        Assert.False(WorkAreaPlacement.IsReachable(titleArea, TwoMonitors[..1]));
    }

    [Fact]
    public void Center_PlacesTheWindowInTheMiddleAndNeverOutsideTheWorkArea()
    {
        Assert.Equal((740, 370), WorkAreaPlacement.Center(TwoMonitors[0], 440, 300));
        Assert.Equal((1920, 0), WorkAreaPlacement.Center(TwoMonitors[1], 5000, 5000));
    }

    [Fact]
    public void CurrentWorkAreas_ContainThePrimaryOne()
    {
        Assert.Contains(WorkAreaPlacement.PrimaryWorkArea(), WorkAreaPlacement.CurrentWorkAreas());
    }
}
