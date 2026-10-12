using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;
using TMHue.Windows.Sampling;
using Xunit;

namespace TMHue.IntegrationTests;

public sealed class FrozenScreenSamplerTests
{
    private const int Width = 96;
    private const int Height = 64;

    [Fact]
    public void DisplayedImageAndPixelReads_ShareTheSameSnapshot()
    {
        var layout = CreateLayout();
        Assert.True(FrozenScreenSampler.TryCreate(layout, out var created));
        using var sampler = created;
        var frame = Assert.Single(sampler!.Frames);
        var bounds = frame.Monitor.Bounds;

        var displayed = CopyPixels(frame);

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            Assert.True(sampler.TryReadPixel(bounds.Left + x, bounds.Top + y, out var color));
            var i = (y * Width + x) * 4;
            Assert.Equal(new RgbColor(displayed[i + 2], displayed[i + 1], displayed[i]), color);
        }
    }

    [Fact]
    public async Task SnapshotCreatedOnAWorkerThread_IsUsableFromAnotherThread()
    {
        var layout = CreateLayout();
        var sampler = await Task.Run(() =>
            FrozenScreenSampler.TryCreate(layout, out var created) ? created : null);
        Assert.NotNull(sampler);
        using (sampler)
        {
            var frame = Assert.Single(sampler.Frames);
            Assert.True(frame.Image.IsFrozen);
            Assert.Equal(Width, frame.Image.PixelWidth);
            Assert.Equal(Height, frame.Image.PixelHeight);
            Assert.Equal(Width * Height * 4, CopyPixels(frame).Length);
            Assert.True(sampler.TryReadRegion(frame.Monitor.Bounds.Left + 8, frame.Monitor.Bounds.Top + 8, 5, out var sample));
            Assert.True(sample!.Valid[2, 2]);
        }
    }

    [Fact]
    public void Dispose_WipesThePixelsStillHeldByTheDisplayedImage()
    {
        var layout = CreateLayout();
        Assert.True(FrozenScreenSampler.TryCreate(layout, out var sampler));
        var frame = Assert.Single(sampler!.Frames);
        var bounds = frame.Monitor.Bounds;

        sampler.Dispose();

        Assert.All(CopyPixels(frame), value => Assert.Equal(0, value));
        Assert.Empty(sampler.Frames);
        Assert.Throws<ObjectDisposedException>(() => sampler.TryReadPixel(bounds.Left, bounds.Top, out _));
    }

    [Fact]
    public void PixelsOutsideTheSnapshot_AreReportedAsUnavailable()
    {
        var layout = CreateLayout();
        Assert.True(FrozenScreenSampler.TryCreate(layout, out var created));
        using var sampler = created;
        var bounds = Assert.Single(sampler!.Frames).Monitor.Bounds;

        Assert.False(sampler.TryReadPixel(bounds.Left - 1, bounds.Top, out _));
        Assert.False(sampler.TryReadPixel(bounds.Right, bounds.Bottom, out _));
        Assert.True(sampler.TryReadPixel(bounds.Right - 1, bounds.Bottom - 1, out _));
    }

    private static byte[] CopyPixels(FrozenMonitorFrame frame)
    {
        var pixels = new byte[Width * Height * 4];
        frame.Image.CopyPixels(pixels, Width * 4, 0);
        return pixels;
    }

    // A small slice of the leftmost monitor stands in for a whole one: same capture path,
    // without allocating a full-screen snapshot per test.
    private static MonitorLayout CreateLayout()
    {
        var screen = System.Windows.Forms.Screen.AllScreens
            .OrderBy(s => s.Bounds.Left).ThenBy(s => s.Bounds.Top).First().Bounds;
        return new MonitorLayout(new[] { new MonitorArea("slice", new(screen.Left, screen.Top, Width, Height)) });
    }
}
