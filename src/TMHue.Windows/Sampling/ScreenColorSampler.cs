using System.ComponentModel;
using System.Runtime.InteropServices;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.ValueObjects;
using TMHue.Core.Services;
using TMHue.Windows.Display;
using TMHue.Windows.Native;

namespace TMHue.Windows.Sampling;

/// <summary>Bounded GDI captures on the UI thread. Reuses buffers, clips to actual monitors,
/// and never returns a previous frame when a native operation fails.</summary>
public sealed class ScreenColorSampler : IScreenColorSampler, IDisposable
{
    private System.Drawing.Bitmap? _bitmap;
    private System.Drawing.Graphics? _graphics;
    private byte[]? _bytes;
    private ScreenSample? _sample;
    private bool _disposed;
    private readonly ScreenLineSampler _line = new();
    private MonitorLayout? _sessionLayout;

    /// <summary>Pins the monitor layout for a capture session, so the per-frame reads stop
    /// re-enumerating the displays. Outside a session every read resolves a fresh layout.</summary>
    public void BeginSession(MonitorLayout layout) => Volatile.Write(ref _sessionLayout, layout);

    /// <summary>Unpins <paramref name="layout"/>; a no-op when a newer session already replaced it.</summary>
    public void EndSession(MonitorLayout layout) => Interlocked.CompareExchange(ref _sessionLayout, null, layout);

    public bool TryReadPixel(int physicalX, int physicalY, out RgbColor color)
    {
        color = default;
        if (!TryReadRegion(physicalX, physicalY, 1, out var sample)) return false;
        return sample!.TrySelectColor(1, out color);
    }

    public bool TryReadRegion(int centerX, int centerY, int side, out ScreenSample? sample)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        sample = null;
        if (side is < 1 or > 11 || side % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(side));
        EnsureBuffers(side);
        _sample!.Clear();
        var layout = Volatile.Read(ref _sessionLayout) ?? new MonitorLayout();
        if (layout.At(centerX, centerY) is null) return false;
        var requested = new System.Drawing.Rectangle(centerX - side / 2, centerY - side / 2, side, side);
        try
        {
            _graphics!.Clear(System.Drawing.Color.Black);
            foreach (var monitor in layout.Monitors)
            {
                var bounds = monitor.Bounds;
                var clipped = System.Drawing.Rectangle.Intersect(requested,
                    new(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
                if (clipped.IsEmpty) continue;
                var dx = clipped.Left - requested.Left;
                var dy = clipped.Top - requested.Top;
                _graphics.CopyFromScreen(clipped.Left, clipped.Top, dx, dy, clipped.Size,
                    System.Drawing.CopyPixelOperation.SourceCopy);
                for (var row = dy; row < dy + clipped.Height; row++)
                for (var col = dx; col < dx + clipped.Width; col++)
                    _sample.Valid[row, col] = true;
            }

            var data = _bitmap!.LockBits(new System.Drawing.Rectangle(0, 0, side, side),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(data.Scan0, _bytes!, 0, data.Stride * side);
                for (var row = 0; row < side; row++)
                for (var col = 0; col < side; col++)
                {
                    if (!_sample.Valid[row, col]) continue;
                    var i = row * data.Stride + col * 4;
                    _sample.Pixels[row, col] = new RgbColor(_bytes![i + 2], _bytes[i + 1], _bytes[i]);
                }
            }
            finally { _bitmap.UnlockBits(data); }
            sample = _sample;
            return true;
        }
        catch (Exception error) when (error is Win32Exception or ExternalException or ArgumentException)
        {
            _sample.Clear();
            return false;
        }
    }

    public bool TryFindNextColorChange(int startX, int startY, int stepX, int stepY,
        int maxSteps, out (int X, int Y) result)
        => TryFindNextColorChange(startX, startY, stepX, stepY, maxSteps, 0, out result);

    public bool TryFindNextColorChange(int startX, int startY, int stepX, int stepY,
        int maxSteps, int tolerance, out (int X, int Y) result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        result = default;
        if (Math.Abs(stepX) + Math.Abs(stepY) != 1 || maxSteps is < 1 or > 80 || tolerance is < 0 or > 32)
            throw new ArgumentOutOfRangeException(nameof(maxSteps));
        return _line.TryFind(startX, startY, stepX, stepY, maxSteps, tolerance,
            Volatile.Read(ref _sessionLayout) ?? new MonitorLayout(), out result);
    }

    private void EnsureBuffers(int side)
    {
        if (_sample?.Side == side) return;
        _graphics?.Dispose(); _bitmap?.Dispose();
        _bitmap = new(side, side, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        _graphics = System.Drawing.Graphics.FromImage(_bitmap);
        _bytes = new byte[side * side * 4];
        _sample = new(side);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _line.Dispose();
        _graphics?.Dispose(); _bitmap?.Dispose();
        _graphics = null; _bitmap = null; _bytes = null; _sample = null;
    }
}
