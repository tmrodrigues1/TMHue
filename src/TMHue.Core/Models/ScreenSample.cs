using TMHue.Core.ValueObjects;

namespace TMHue.Core.Models;

/// <summary>A bounded capture and its validity mask. Buffers may be reused on the next read;
/// consume synchronously. Invalid cells are never candidates for color selection.</summary>
public sealed class ScreenSample
{
    public ScreenSample(int side)
    {
        if (side is < 1 or > 11 || side % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(side));
        Side = side;
        Pixels = new RgbColor[side, side];
        Valid = new bool[side, side];
    }

    public int Side { get; }
    public RgbColor[,] Pixels { get; }
    public bool[,] Valid { get; }
    public string SourceDescription { get; set; } = "GDI · RGB 8-bit";
    public CaptureColorOutput ColorOutput { get; set; }
    public bool? AdvancedColorActive { get; set; }
    public bool IsFrozen { get; set; }

    public void Clear()
    {
        Array.Clear(Pixels);
        Array.Clear(Valid);
    }

    public bool TrySelectColor(int sampleSide, out RgbColor color)
    {
        color = default;
        if (sampleSide is not (1 or 5 or 11) || sampleSide > Side)
            throw new ArgumentOutOfRangeException(nameof(sampleSide));
        var center = Side / 2;
        if (!Valid[center, center]) return false;
        if (sampleSide == 1)
        {
            color = Pixels[center, center];
            return true;
        }

        // Median is only a target: select an actual captured pixel nearest to it.
        // This costs O(n log n + n), bounded to 121 cells, instead of an O(n²) medoid.
        Span<byte> reds = stackalloc byte[121];
        Span<byte> greens = stackalloc byte[121];
        Span<byte> blues = stackalloc byte[121];
        var offset = (Side - sampleSide) / 2;
        var end = offset + sampleSide;
        var count = 0;
        for (var row = offset; row < end; row++)
        for (var col = offset; col < end; col++)
        {
            if (!Valid[row, col]) continue;
            var pixel = Pixels[row, col];
            reds[count] = pixel.Red;
            greens[count] = pixel.Green;
            blues[count++] = pixel.Blue;
        }
        reds[..count].Sort(); greens[..count].Sort(); blues[..count].Sort();
        var median = new RgbColor(reds[count / 2], greens[count / 2], blues[count / 2]);
        var bestDistance = int.MaxValue;
        var bestPosition = int.MaxValue;
        for (var row = offset; row < end; row++)
        for (var col = offset; col < end; col++)
        {
            if (!Valid[row, col]) continue;
            var pixel = Pixels[row, col];
            var dr = pixel.Red - median.Red;
            var dg = pixel.Green - median.Green;
            var db = pixel.Blue - median.Blue;
            var distance = dr * dr + dg * dg + db * db;
            var position = (row - center) * (row - center) + (col - center) * (col - center);
            if (distance > bestDistance || (distance == bestDistance && position >= bestPosition)) continue;
            bestDistance = distance;
            bestPosition = position;
            color = pixel;
        }
        return true;
    }
}
