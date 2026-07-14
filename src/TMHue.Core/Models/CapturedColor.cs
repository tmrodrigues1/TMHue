using TMHue.Core.ValueObjects;

namespace TMHue.Core.Models;

public sealed record CapturedColor(
    string Hex,
    byte Red,
    byte Green,
    byte Blue,
    DateTimeOffset CapturedAt)
{
    public bool IsPinned { get; init; }

    public static CapturedColor FromRgb(RgbColor rgb, bool uppercaseHex, DateTimeOffset? capturedAt = null) =>
        new(rgb.ToHex(uppercaseHex), rgb.Red, rgb.Green, rgb.Blue, capturedAt ?? DateTimeOffset.Now);

    public RgbColor ToRgb() => new(Red, Green, Blue);
}
