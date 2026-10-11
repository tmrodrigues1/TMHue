using TMHue.Core.ValueObjects;

namespace TMHue.Core.Services;

public static class ColorChangeSearch
{
    public static bool Differs(RgbColor first, RgbColor next, int tolerance) =>
        Math.Abs(first.Red - next.Red) > tolerance || Math.Abs(first.Green - next.Green) > tolerance
        || Math.Abs(first.Blue - next.Blue) > tolerance;
}
