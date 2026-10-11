using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using Xunit;

namespace TMHue.UnitTests;

public sealed class ScreenSampleTests
{
    private static readonly RgbColor Blue = new(0x3A, 0x10, 0xE0);

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(11)]
    public void SolidReference_IsUnchanged(int side)
    {
        var sample = Fill(11, Blue);
        Assert.True(sample.TrySelectColor(side, out var selected));
        Assert.Equal(Blue, selected);
    }

    [Fact]
    public void Exact_ReturnsCenterEvenWhenSurroundedByWhite()
    {
        var sample = Fill(11, new(255, 255, 255));
        sample.Pixels[5, 5] = Blue;
        Assert.True(sample.TrySelectColor(1, out var selected));
        Assert.Equal(Blue, selected);
        Assert.True(sample.TrySelectColor(5, out selected));
        Assert.Equal(new RgbColor(255, 255, 255), selected);
    }

    [Fact]
    public void RegionalMedian_CannotInventBlackFromRgbPrimaries()
    {
        var sample = Fill(5, new(255, 0, 0));
        var colors = new[] { new RgbColor(255, 0, 0), new(0, 255, 0), new(0, 0, 255) };
        for (var row = 0; row < 5; row++)
        for (var col = 0; col < 5; col++) sample.Pixels[row, col] = colors[(row * 5 + col) % 3];
        Assert.Equal(default, RgbColor.MedianOf(sample.Pixels, 5));
        Assert.True(sample.TrySelectColor(5, out var selected));
        Assert.Contains(selected, colors);
        Assert.Equal(sample.Pixels[2, 2], selected); // equal color distances favor the center
    }

    [Fact]
    public void PartialRegion_IgnoresInvalidPixels()
    {
        var sample = new ScreenSample(11);
        for (var row = 5; row < 11; row++)
        for (var col = 5; col < 11; col++)
        {
            sample.Valid[row, col] = true;
            sample.Pixels[row, col] = Blue;
        }
        Assert.True(sample.TrySelectColor(11, out var selected));
        Assert.Equal(Blue, selected);
    }

    [Fact]
    public void InvalidCenter_RejectsRegionInsteadOfSelectingNeighbor()
    {
        var sample = Fill(5, Blue);
        sample.Valid[2, 2] = false;
        Assert.False(sample.TrySelectColor(5, out _));
    }

    [Fact]
    public void Subregion_DoesNotConsiderOuterPixels()
    {
        var sample = Fill(11, new(255, 255, 255));
        for (var row = 3; row < 8; row++)
        for (var col = 3; col < 8; col++) sample.Pixels[row, col] = Blue;
        Assert.True(sample.TrySelectColor(5, out var selected));
        Assert.Equal(Blue, selected);
    }

    [Fact]
    public void Clear_DiscardsBothPixelsAndValidity()
    {
        var sample = Fill(11, Blue);
        sample.Clear();
        Assert.False(sample.TrySelectColor(1, out _));
        Assert.All(sample.Pixels.Cast<RgbColor>(), c => Assert.Equal(default, c));
        Assert.All(sample.Valid.Cast<bool>(), value => Assert.False(value));
    }

    private static ScreenSample Fill(int side, RgbColor color)
    {
        var sample = new ScreenSample(side);
        for (var row = 0; row < side; row++)
        for (var col = 0; col < side; col++)
        {
            sample.Pixels[row, col] = color;
            sample.Valid[row, col] = true;
        }
        return sample;
    }
}
