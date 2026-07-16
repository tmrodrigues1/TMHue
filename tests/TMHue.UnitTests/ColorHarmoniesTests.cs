using TMHue.Core.ValueObjects;
using Xunit;

namespace TMHue.UnitTests;

public class ColorHarmoniesTests
{
    private static readonly RgbColor Red = new(0xFF, 0x00, 0x00);

    [Fact]
    public void Complementary_OfRed_IsCyan()
    {
        var result = ColorHarmonies.Complementary(Red);

        Assert.Equal(2, result.Length);
        Assert.Equal(Red, result[0]);
        Assert.Equal(new RgbColor(0x00, 0xFF, 0xFF), result[1]);
    }

    [Fact]
    public void Triadic_OfRed_IsGreenAndBlue()
    {
        var result = ColorHarmonies.Triadic(Red);

        Assert.Equal(3, result.Length);
        Assert.Equal(Red, result[0]);
        Assert.Equal(new RgbColor(0x00, 0xFF, 0x00), result[1]);
        Assert.Equal(new RgbColor(0x00, 0x00, 0xFF), result[2]);
    }

    [Fact]
    public void Analogous_KeepsBaseInTheMiddle()
    {
        var result = ColorHarmonies.Analogous(Red);

        Assert.Equal(3, result.Length);
        Assert.Equal(Red, result[1]);
    }

    [Fact]
    public void Tetradic_ReturnsFourDistinctHues()
    {
        var result = ColorHarmonies.Tetradic(Red);

        Assert.Equal(4, result.Length);
        Assert.Equal(4, result.Distinct().Count());
    }

    [Fact]
    public void Tints_GetProgressivelyLighter_WithoutReachingWhite()
    {
        var tints = ColorHarmonies.Tints(Red);

        Assert.Equal(5, tints.Length);
        var previous = Red.ToHsl().Lightness;
        foreach (var tint in tints)
        {
            var lightness = tint.ToHsl().Lightness;
            Assert.True(lightness > previous);
            previous = lightness;
        }
        Assert.NotEqual(new RgbColor(0xFF, 0xFF, 0xFF), tints[^1]);
    }

    [Fact]
    public void Shades_GetProgressivelyDarker_WithoutReachingBlack()
    {
        var shades = ColorHarmonies.Shades(Red);

        Assert.Equal(5, shades.Length);
        var previous = Red.ToHsl().Lightness;
        foreach (var shade in shades)
        {
            var lightness = shade.ToHsl().Lightness;
            Assert.True(lightness < previous);
            previous = lightness;
        }
        Assert.NotEqual(new RgbColor(0x00, 0x00, 0x00), shades[^1]);
    }
}
