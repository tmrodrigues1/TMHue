using TMHue.Windows.Display;
using TMHue.Windows.Sampling;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class MonitorNavigationTests
{
    [Fact]
    public void Arrows_MoveExactlyOnePixelAndCrossAdjacentMonitors()
    {
        var layout = new MonitorLayout(new[] { new MonitorArea("left", new(-100, 0, 100, 100)), new("right", new(0, 0, 100, 100)) });
        Assert.Equal((0, 20), layout.LimitMovement(-1, 20, 1, 0, 1));
        Assert.Equal((-1, 20), layout.LimitMovement(0, 20, -1, 0, 1));
    }

    [Fact]
    public void Jump_CannotCrossMonitorGap()
    {
        var layout = new MonitorLayout(new[] { new MonitorArea("left", new(0, 0, 100, 100)), new("right", new(120, 0, 100, 100)) });
        Assert.Null(layout.At(105, 20));
        Assert.Equal((99, 20), layout.LimitMovement(90, 20, 1, 0, 80));
    }

    [Fact]
    public void FrozenMemoryLimit_IsEnforcedBeforeAllocating()
    {
        var layout = new MonitorLayout(new[] { new MonitorArea("huge", new(0, 0, 100000, 100000)) });
        Assert.False(FrozenScreenSampler.TryCreate(layout, out var sampler));
        Assert.Null(sampler);
    }

    [Fact]
    public void CancelledSnapshot_DoesNotStartCaptureOrAllocateFrames()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var layout = new MonitorLayout(new[] { new MonitorArea("test", new(0, 0, 100, 100)) });
        Assert.Throws<OperationCanceledException>(() => FrozenScreenSampler.TryCreate(layout, out _, cancellation.Token));
    }
}
