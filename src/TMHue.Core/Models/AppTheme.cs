namespace TMHue.Core.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum CopyFormat
{
    Hex,
    Rgb,
    Hsl
}

public static class CopyFormatExtensions
{
    public static string ToLabel(this CopyFormat format) => format switch
    {
        CopyFormat.Rgb => "RGB",
        CopyFormat.Hsl => "HSL",
        _ => "HEX"
    };
}
