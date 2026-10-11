using TMHue.Windows.Display;
using TMHue.Windows.Sampling;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class ScreenColorSamplerTests
{
    [Theory]
    [InlineData(-1200, -300)]
    [InlineData(1920, 1080)]
    public void ClickMessage_KeepsSignedEventCoordinates(int x, int y)
    {
        var packed = (nint)((uint)(ushort)x | ((uint)(ushort)y << 16));
        Assert.Equal((x, y), MouseMessagePosition.Decode(packed));
    }
    [Fact]
    public void FailedRead_ReturnsNoSampleAndClearsBorrowedBuffer()
    {
        using var sampler = new ScreenColorSampler();
        var bounds = GetLeftmostMonitor();
        Assert.True(sampler.TryReadRegion(bounds.Left, bounds.Top, 11, out var borrowed));
        Assert.False(sampler.TryReadRegion(int.MaxValue, int.MaxValue, 11, out var failed));
        Assert.Null(failed);
        Assert.All(borrowed!.Valid.Cast<bool>(), valid => Assert.False(valid));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(1000000)]
    public void InvalidDimensions_AreRejectedBeforeAllocation(int side)
    {
        using var sampler = new ScreenColorSampler();
        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.TryReadRegion(0, 0, side, out _));
    }

    [Fact]
    public void ScreenCorner_InvalidCellsAreClearedAndExcluded()
    {
        using var sampler = new ScreenColorSampler();
        var bounds = GetLeftmostMonitor();
        Assert.True(sampler.TryReadRegion(bounds.Left, bounds.Top, 11, out var sample));
        Assert.True(sample!.Valid[5, 5]);
        Assert.False(sample.Valid[0, 0]);
        Assert.Equal(default, sample.Pixels[0, 0]);
        Assert.True(sample.TrySelectColor(11, out _));
    }

    [Fact]
    public void Exclusion_InvalidWindowReportsFailure()
    {
        Assert.False(CaptureExclusion.Apply(nint.Zero).Succeeded);
    }

    private static System.Drawing.Rectangle GetLeftmostMonitor() => System.Windows.Forms.Screen.AllScreens
        .OrderBy(screen => screen.Bounds.Left).ThenBy(screen => screen.Bounds.Top).First().Bounds;
}
