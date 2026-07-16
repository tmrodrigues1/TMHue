using System.Globalization;
using System.Text.RegularExpressions;
using TMHue.Core.Models;

namespace TMHue.Core.ValueObjects;

/// <summary>Immutable RGB triplet with hex formatting. Never round-trips through floating point.</summary>
public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    public string ToHex(bool uppercase = true)
    {
        var hex = $"#{Red:X2}{Green:X2}{Blue:X2}";
        return uppercase ? hex : hex.ToLowerInvariant();
    }

    public string ToRgbString() => $"rgb({Red}, {Green}, {Blue})";

    /// <summary>Standard HSL conversion (hue in degrees, saturation/lightness as percentages).</summary>
    public string ToHslString()
    {
        var (hue, saturation, lightness) = ToHsl();
        return $"hsl({Math.Round(hue)}, {Math.Round(saturation * 100)}%, {Math.Round(lightness * 100)}%)";
    }

    /// <summary>Structured HSL components (hue 0-360, saturation/lightness 0-1), for callers that
    /// need to do color math (e.g. harmony generation) rather than display a formatted string.</summary>
    public (double Hue, double Saturation, double Lightness) ToHsl()
    {
        var r = Red / 255.0;
        var g = Green / 255.0;
        var b = Blue / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var lightness = (max + min) / 2.0;

        double hue = 0;
        double saturation = 0;

        if (delta > 0.0001)
        {
            saturation = delta / (1 - Math.Abs(2 * lightness - 1));

            if (max == r)
                hue = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                hue = 60 * ((b - r) / delta + 2);
            else
                hue = 60 * ((r - g) / delta + 4);

            if (hue < 0) hue += 360;
        }

        return (hue, saturation, lightness);
    }

    public string Format(CopyFormat format, bool uppercaseHex = true) => format switch
    {
        CopyFormat.Rgb => ToRgbString(),
        CopyFormat.Hsl => ToHslString(),
        _ => ToHex(uppercaseHex)
    };

    public static bool TryParseHex(string? value, out RgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var span = value.Trim();
        if (span.StartsWith('#')) span = span[1..];
        if (span.Length != 6) return false;

        if (!byte.TryParse(span[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)) return false;
        if (!byte.TryParse(span[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)) return false;
        if (!byte.TryParse(span[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b)) return false;

        color = new RgbColor(r, g, b);
        return true;
    }

    private static readonly Regex RgbFunctionPattern = new(
        @"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*[\d.]+\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HslFunctionPattern = new(
        @"^hsla?\(\s*(-?\d{1,3}(?:\.\d+)?)\s*,\s*(\d{1,3}(?:\.\d+)?)%\s*,\s*(\d{1,3}(?:\.\d+)?)%\s*(?:,\s*[\d.]+\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Parses a color in any of the formats <see cref="Format"/> can produce (HEX, RGB or
    /// HSL functional notation), so user-facing inputs aren't tied to a single format.</summary>
    public static bool TryParse(string? value, out RgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        return TryParseRgbFunction(trimmed, out color)
            || TryParseHslFunction(trimmed, out color)
            || TryParseHex(trimmed, out color);
    }

    private static bool TryParseRgbFunction(string value, out RgbColor color)
    {
        color = default;
        var match = RgbFunctionPattern.Match(value);
        if (!match.Success) return false;

        if (!byte.TryParse(match.Groups[1].Value, out var r)) return false;
        if (!byte.TryParse(match.Groups[2].Value, out var g)) return false;
        if (!byte.TryParse(match.Groups[3].Value, out var b)) return false;

        color = new RgbColor(r, g, b);
        return true;
    }

    private static bool TryParseHslFunction(string value, out RgbColor color)
    {
        color = default;
        var match = HslFunctionPattern.Match(value);
        if (!match.Success) return false;

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)) return false;
        if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return false;
        if (!double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var l)) return false;

        // Saturation/lightness outside 0-100% aren't a valid color (unlike hue, which is cyclic
        // and wraps); reject them here instead of silently clamping, matching how an out-of-range
        // RGB channel (e.g. rgb(300, 0, 0)) is already rejected.
        if (s is < 0 or > 100 || l is < 0 or > 100) return false;

        color = FromHsl(h, s / 100.0, l / 100.0);
        return true;
    }

    public static RgbColor FromHsl(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360) + 360) % 360;

        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - c / 2;

        var (r1, g1, b1) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return new RgbColor(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    public override string ToString() => ToHex();

    /// <summary>Per-channel median of a square pixel region. Median rather than mean because a
    /// handful of outlier pixels (e.g. a thin line of text over a solid background) shouldn't
    /// drag the result away from the dominant color the way an average would.</summary>
    public static RgbColor MedianOf(RgbColor[,] region, int side) => MedianOf(region, side, side);

    /// <summary>Same median, computed over the centered <paramref name="sampleSide"/>² sub-square
    /// of a larger <paramref name="regionSide"/>² region — lets one screen capture feed both the
    /// magnifier and a smaller sampling area without a second capture. Runs on every mouse move
    /// during a capture, hence stackalloc instead of per-call arrays (sample sizes are small and
    /// bounded: at most 11x11 today).</summary>
    public static RgbColor MedianOf(RgbColor[,] region, int regionSide, int sampleSide)
    {
        var count = sampleSide * sampleSide;
        Span<byte> reds = count <= 256 ? stackalloc byte[count] : new byte[count];
        Span<byte> greens = count <= 256 ? stackalloc byte[count] : new byte[count];
        Span<byte> blues = count <= 256 ? stackalloc byte[count] : new byte[count];

        var offset = (regionSide - sampleSide) / 2;
        var i = 0;
        for (var row = 0; row < sampleSide; row++)
        {
            for (var col = 0; col < sampleSide; col++)
            {
                var c = region[offset + row, offset + col];
                reds[i] = c.Red;
                greens[i] = c.Green;
                blues[i] = c.Blue;
                i++;
            }
        }

        reds.Sort();
        greens.Sort();
        blues.Sort();

        var mid = count / 2;
        return new RgbColor(reds[mid], greens[mid], blues[mid]);
    }
}
