using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using Xunit;

namespace TMHue.UnitTests;

public sealed class PickerGeometryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2)]
    public void Lens_RemainsInsideNegativeCoordinateMonitor(double scale)
    {
        var b = new PixelBounds(-1920, -1080, 1920, 1080);
        var width = (int)Math.Ceiling(200 * scale); var height = (int)Math.Ceiling(260 * scale);
        foreach (var point in new[] { (-1920, -1080), (-1, -1), (-1000, -800) })
        {
            var placed = b.PlaceLens(point.Item1, point.Item2, width, height);
            Assert.True(b.Contains(placed.X, placed.Y));
            Assert.True(placed.X + width <= b.Right);
            Assert.True(placed.Y + height <= b.Bottom);
        }
    }

    [Fact]
    public void Bounds_AreHalfOpenSoAdjacentMonitorsDoNotShareAPixel()
    {
        var left = new PixelBounds(-1920, 0, 1920, 1080);
        var right = new PixelBounds(0, 0, 1920, 1080);
        Assert.True(left.Contains(-1, 0)); Assert.False(left.Contains(0, 0));
        Assert.True(right.Contains(0, 0)); Assert.False(right.Contains(1920, 0));
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(2, 3, true)]
    [InlineData(32, 32, false)]
    public void Tolerance_UsesDifferenceFromStartingColor(int tolerance, int difference, bool expected)
    {
        var start = new RgbColor(58, 16, 224);
        Assert.Equal(expected, ColorChangeSearch.Differs(start, start with { Red = (byte)(58 + difference) }, tolerance));
    }
}
