using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using Xunit;

namespace TMHue.UnitTests;

public class PaletteExtractorTests
{
    [Fact]
    public void Extract_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(PaletteExtractor.Extract(Array.Empty<RgbColor>()));
    }

    [Fact]
    public void Extract_SingleFlatColor_ReturnsThatColorOnce()
    {
        var pixels = Enumerable.Repeat(new RgbColor(10, 20, 30), 100).ToArray();

        var palette = PaletteExtractor.Extract(pixels, 5);

        Assert.Single(palette);
        Assert.Equal(new RgbColor(10, 20, 30), palette[0]);
    }

    [Fact]
    public void Extract_OrdersByDominance()
    {
        var red = new RgbColor(255, 0, 0);
        var blue = new RgbColor(0, 0, 255);
        var pixels = Enumerable.Repeat(red, 300)
            .Concat(Enumerable.Repeat(blue, 100))
            .ToArray();

        var palette = PaletteExtractor.Extract(pixels, 2);

        Assert.Equal(2, palette.Count);
        Assert.Equal(red, palette[0]);
        Assert.Equal(blue, palette[1]);
    }

    [Fact]
    public void Extract_NeverReturnsMoreThanRequested()
    {
        var random = new Random(42);
        var pixels = Enumerable.Range(0, 5000)
            .Select(_ => new RgbColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)))
            .ToArray();

        var palette = PaletteExtractor.Extract(pixels, 5);

        Assert.Equal(5, palette.Count);
    }

    [Fact]
    public void Extract_DistinctClusters_RecoversEachCluster()
    {
        var colors = new[]
        {
            new RgbColor(250, 10, 10),
            new RgbColor(10, 250, 10),
            new RgbColor(10, 10, 250),
            new RgbColor(240, 240, 10),
            new RgbColor(10, 240, 240)
        };
        var pixels = colors.SelectMany(c => Enumerable.Repeat(c, 200)).ToArray();

        var palette = PaletteExtractor.Extract(pixels, 5);

        Assert.Equal(5, palette.Count);
        foreach (var expected in colors)
            Assert.Contains(palette, actual =>
                Math.Abs(actual.Red - expected.Red) <= 5 &&
                Math.Abs(actual.Green - expected.Green) <= 5 &&
                Math.Abs(actual.Blue - expected.Blue) <= 5);
    }
}
