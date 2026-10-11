using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;

namespace TMHue.Windows.Sampling;

public sealed record FrozenMonitorFrame(MonitorArea Monitor, byte[] Pixels, BitmapSource Image);

/// <summary>Exact physical pixels captured before picker windows exist. Snapshots stay in
/// memory only, have a hard allocation budget and are discarded on every selection exit.</summary>
public sealed class FrozenScreenSampler : IScreenColorSampler, IDisposable
{
    public const long MaxMemoryBytes = 256L * 1024 * 1024;
    private ScreenSample? _sample;
    private bool _disposed;
    public IReadOnlyList<FrozenMonitorFrame> Frames { get; private set; } = Array.Empty<FrozenMonitorFrame>();

    public static bool TryCreate(MonitorLayout layout, out FrozenScreenSampler? sampler, CancellationToken cancellation = default)
    {
        sampler = null;
        cancellation.ThrowIfCancellationRequested();
        var bytesNeeded = layout.Monitors.Sum(m => (long)m.Bounds.Width * m.Bounds.Height * 12);
        if (bytesNeeded <= 0 || bytesNeeded > MaxMemoryBytes) return false;
        var result = new FrozenScreenSampler();
        var frames = new List<FrozenMonitorFrame>();
        try
        {
            foreach (var monitor in layout.Monitors)
            {
                cancellation.ThrowIfCancellationRequested();
                var b = monitor.Bounds;
                using var bitmap = new System.Drawing.Bitmap(b.Width, b.Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(b.Left, b.Top, 0, 0, new(b.Width, b.Height));
                cancellation.ThrowIfCancellationRequested();
                var bytes = new byte[checked(b.Width * b.Height * 4)];
                var data = bitmap.LockBits(new(0, 0, b.Width, b.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try { Marshal.Copy(data.Scan0, bytes, 0, bytes.Length); }
                finally { bitmap.UnlockBits(data); }
                var image = BitmapSource.Create(b.Width, b.Height, 96, 96, PixelFormats.Bgr32,
                    null, bytes, b.Width * 4);
                image.Freeze();
                frames.Add(new(monitor, bytes, image));
            }
            cancellation.ThrowIfCancellationRequested();
            result.Frames = frames;
            sampler = result;
            return true;
        }
        catch (OperationCanceledException)
        {
            foreach (var frame in frames) Array.Clear(frame.Pixels);
            result.Dispose();
            throw;
        }
        catch (Exception error) when (error is Win32Exception or ExternalException or OutOfMemoryException)
        {
            foreach (var frame in frames) Array.Clear(frame.Pixels);
            result.Dispose();
            return false;
        }
    }

    public bool TryReadPixel(int x, int y, out RgbColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        color = default;
        foreach (var frame in Frames)
        {
            var b = frame.Monitor.Bounds;
            if (!b.Contains(x, y)) continue;
            var offset = ((y - b.Top) * b.Width + x - b.Left) * 4;
            color = new(frame.Pixels[offset + 2], frame.Pixels[offset + 1], frame.Pixels[offset]);
            return true;
        }
        return false;
    }

    public bool TryReadRegion(int x, int y, int side, out ScreenSample? sample)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        sample = null;
        if (side is < 1 or > 11 || side % 2 == 0) throw new ArgumentOutOfRangeException(nameof(side));
        if (_sample?.Side != side) _sample = new(side);
        _sample.Clear();
        if (!TryReadPixel(x, y, out _)) return false;
        for (var row = 0; row < side; row++)
        for (var col = 0; col < side; col++)
        {
            if (!TryReadPixel(x + col - side / 2, y + row - side / 2, out var color)) continue;
            _sample.Pixels[row, col] = color; _sample.Valid[row, col] = true;
        }
        _sample.SourceDescription = "GDI · RGB 8-bit · frozen";
        sample = _sample;
        return true;
    }

    public bool TryFindNextColorChange(int x, int y, int dx, int dy, int steps, out (int X, int Y) result) =>
        TryFindNextColorChange(x, y, dx, dy, steps, 0, out result);

    public bool TryFindNextColorChange(int x, int y, int dx, int dy, int steps, int tolerance,
        out (int X, int Y) result)
    {
        result = default;
        if (Math.Abs(dx) + Math.Abs(dy) != 1 || steps is < 1 or > 80 || tolerance is < 0 or > 32)
            throw new ArgumentOutOfRangeException(nameof(steps));
        if (!TryReadPixel(x, y, out var start)) return false;
        for (var i = 1; i <= steps; i++)
        {
            if (!TryReadPixel(x + dx * i, y + dy * i, out var next)) return false;
            if (!ColorChangeSearch.Differs(start, next, tolerance)) continue;
            result = (x + dx * i, y + dy * i);
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var frame in Frames) Array.Clear(frame.Pixels);
        Frames = Array.Empty<FrozenMonitorFrame>();
        _sample?.Clear(); _sample = null;
    }
}
