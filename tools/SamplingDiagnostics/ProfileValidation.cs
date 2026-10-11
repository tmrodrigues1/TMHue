using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Sampling;

namespace SamplingDiagnostics;

internal static class ProfileValidation
{
    public static object Compare(string? srgbPath)
    {
        var adobePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "spool", "drivers", "color", "AdobeRGB1998.icc");
        if (srgbPath is null || !File.Exists(adobePath))
            return new { Available = false, Reason = "Adobe RGB or sRGB fixture unavailable" };
        using var transform = IccColorTransform.TryCreate(adobePath, srgbPath)
            ?? throw new InvalidOperationException("ICC transform unavailable");
        var sample = new ScreenSample(1);
        var input = new RgbColor(58, 16, 224);
        sample.Pixels[0, 0] = input; sample.Valid[0, 0] = true;
        if (!transform.TryConvert(sample, new(0, 0, 1, 1), 0, 0))
            throw new InvalidOperationException("ICC fixture conversion failed");
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null,
            new byte[] { input.Blue, input.Green, input.Red }, 3);
        var reference = new ColorConvertedBitmap(bitmap, new ColorContext(new Uri(adobePath)),
            new ColorContext(new Uri(srgbPath)), PixelFormats.Bgr24);
        var bytes = new byte[3];
        reference.CopyPixels(bytes, 3, 0);
        var wpf = new RgbColor(bytes[2], bytes[1], bytes[0]);
        var converted = sample.Pixels[0, 0];
        var difference = Math.Max(Math.Abs(converted.Red - wpf.Red),
            Math.Max(Math.Abs(converted.Green - wpf.Green), Math.Abs(converted.Blue - wpf.Blue)));
        if (difference > 1 || converted == input)
            throw new InvalidOperationException($"ICC reference mismatch: {converted.ToHex()} vs {wpf.ToHex()}");
        return new { Available = true, SourceProfile = Path.GetFileName(adobePath),
            DestinationProfile = Path.GetFileName(srgbPath), Input = input.ToHex(), Converted = converted.ToHex(),
            WpfReference = wpf.ToHex(), MaximumChannelDifference = difference,
            Note = "Fixture only; actual monitor profiles were not changed" };
    }
}
