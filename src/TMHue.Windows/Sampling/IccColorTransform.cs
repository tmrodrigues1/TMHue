using System.Runtime.InteropServices;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Native;

namespace TMHue.Windows.Sampling;

/// <summary>Explicit SDR profile-to-sRGB, relative colorimetric. Never compensates channels
/// or attempts to reconstruct an image's original colors from rendered screen values.</summary>
public sealed unsafe class IccColorTransform : IDisposable
{
    private ProfileHandle? _source, _destination;
    private TransformHandle? _transform;
    private readonly bool _identity;
    private bool _disposed;
    private IccColorTransform(bool identity) => _identity = identity;

    public static IccColorTransform? TryCreate(string source, string destination)
    {
        if (!System.IO.File.Exists(source) || !System.IO.File.Exists(destination)) return null;
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) return new(true);
        var result = new IccColorTransform(false);
        result._source = Open(source); result._destination = Open(destination);
        if (result._source is null || result._destination is null
            || result._source.IsInvalid || result._destination.IsInvalid) { result.Dispose(); return null; }
        nint* profiles = stackalloc nint[2] { result._source.DangerousGetHandle(), result._destination.DangerousGetHandle() };
        uint intent = 1; // INTENT_RELATIVE_COLORIMETRIC
        result._transform = IccNative.CreateMultiProfileTransform(profiles, 2, &intent, 1, 3, 0);
        if (result._transform.IsInvalid) { result.Dispose(); return null; }
        return result;
    }

    private static ProfileHandle? Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 8191) return null;
        var filename = Marshal.StringToCoTaskMemUni(path);
        try
        {
            var profile = new IccNative.Profile { Type = 1, Data = filename, Size = (uint)((path.Length + 1) * 2) };
            return IccNative.OpenColorProfile(ref profile, 1, 1, 3); // read, share-read, OPEN_EXISTING
        }
        finally { Marshal.FreeCoTaskMem(filename); }
    }

    public bool TryConvert(ScreenSample sample, PixelBounds monitor, int captureLeft, int captureTop)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_identity) return true;
        if (_transform is null) return false;
        Span<IccNative.Color> input = stackalloc IccNative.Color[121];
        Span<IccNative.Color> output = stackalloc IccNative.Color[121];
        Span<int> positions = stackalloc int[121];
        var count = 0;
        for (var row = 0; row < sample.Side; row++)
        for (var col = 0; col < sample.Side; col++)
        {
            if (!sample.Valid[row, col] || !monitor.Contains(captureLeft + col, captureTop + row)) continue;
            var c = sample.Pixels[row, col];
            input[count] = new() { Red = (ushort)(c.Red * 257), Green = (ushort)(c.Green * 257), Blue = (ushort)(c.Blue * 257) };
            positions[count++] = row * sample.Side + col;
        }
        if (count == 0) return true;
        fixed (IccNative.Color* first = input, second = output)
            if (!IccNative.TranslateColors(_transform, first, (uint)count, 2, second, 2)) return false;
        for (var i = 0; i < count; i++)
        {
            var c = output[i];
            sample.Pixels[positions[i] / sample.Side, positions[i] % sample.Side] =
                new RgbColor((byte)((c.Red + 128) / 257), (byte)((c.Green + 128) / 257), (byte)((c.Blue + 128) / 257));
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _transform?.Dispose(); _source?.Dispose(); _destination?.Dispose();
    }
}
