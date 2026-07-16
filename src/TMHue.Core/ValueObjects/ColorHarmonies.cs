namespace TMHue.Core.ValueObjects;

/// <summary>Classic color-wheel harmonies plus tint/shade scales, derived from a single base
/// color via HSL hue rotation. Pure math over <see cref="RgbColor"/> — no UI concerns — so the
/// same rules can back any surface (harmony window today, exports tomorrow) and be unit-tested.</summary>
public static class ColorHarmonies
{
    /// <summary>Base color plus its opposite on the color wheel (hue + 180°).</summary>
    public static RgbColor[] Complementary(RgbColor color) => RotateHues(color, 0, 180);

    /// <summary>Neighbors 30° to each side of the base hue, base in the middle.</summary>
    public static RgbColor[] Analogous(RgbColor color) => RotateHues(color, -30, 0, 30);

    /// <summary>Three colors evenly spaced 120° apart, starting at the base hue.</summary>
    public static RgbColor[] Triadic(RgbColor color) => RotateHues(color, 0, 120, 240);

    /// <summary>Four colors evenly spaced 90° apart (square tetrad), starting at the base hue.</summary>
    public static RgbColor[] Tetradic(RgbColor color) => RotateHues(color, 0, 90, 180, 270);

    /// <summary>Five progressively lighter steps toward (but never reaching) pure white.</summary>
    public static RgbColor[] Tints(RgbColor color) => LightnessScale(color, towardWhite: true);

    /// <summary>Five progressively darker steps toward (but never reaching) pure black.</summary>
    public static RgbColor[] Shades(RgbColor color) => LightnessScale(color, towardWhite: false);

    private static RgbColor[] RotateHues(RgbColor color, params int[] offsets)
    {
        var (h, s, l) = color.ToHsl();
        var result = new RgbColor[offsets.Length];
        for (var i = 0; i < offsets.Length; i++)
        {
            // Offset 0 keeps the exact base color (no HSL round-trip drift).
            result[i] = offsets[i] == 0 ? color : RgbColor.FromHsl(h + offsets[i], s, l);
        }
        return result;
    }

    private static RgbColor[] LightnessScale(RgbColor color, bool towardWhite)
    {
        var (h, s, l) = color.ToHsl();
        var result = new RgbColor[5];
        for (var i = 0; i < result.Length; i++)
        {
            // Steps of 1/6th of the remaining range, so the fifth step still stops short of the
            // pure white/black extreme (which would be the same for every base color).
            var fraction = (i + 1) / 6.0;
            var lightness = towardWhite ? l + (1 - l) * fraction : l * (1 - fraction);
            result[i] = RgbColor.FromHsl(h, s, lightness);
        }
        return result;
    }
}
