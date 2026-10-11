using System.ComponentModel;
using System.Runtime.InteropServices;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;

namespace TMHue.Windows.Sampling;

/// <summary>Shift navigation copies at most 81 pixels in one bounded strip instead of
/// synchronously calling GetPixel up to 81 times. Separate buffers preserve preview ownership.</summary>
internal sealed class ScreenLineSampler : IDisposable
{
    private System.Drawing.Bitmap? _bitmap;
    private System.Drawing.Graphics? _graphics;
    private readonly byte[] _pixels = new byte[81 * 4];

    public bool TryFind(int x, int y, int dx, int dy, int steps, int tolerance, out (int X, int Y) result)
    {
        result = default;
        var width = dx == 0 ? 1 : steps + 1;
        var height = dy == 0 ? 1 : steps + 1;
        var left = Math.Min(x, x + dx * steps); var top = Math.Min(y, y + dy * steps);
        var region = new System.Drawing.Rectangle(left, top, width, height);
        var layout = new MonitorLayout();
        if (layout.At(x, y) is null) return false;
        EnsureBuffers(width, height);
        try
        {
            _graphics!.Clear(System.Drawing.Color.Black);
            foreach (var monitor in layout.Monitors)
            {
                var bounds = monitor.Bounds;
                var clipped = System.Drawing.Rectangle.Intersect(region, new(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
                if (clipped.IsEmpty) continue;
                _graphics.CopyFromScreen(clipped.Left, clipped.Top, clipped.Left - left, clipped.Top - top, clipped.Size);
            }
            var data = _bitmap!.LockBits(new(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try { Marshal.Copy(data.Scan0, _pixels, 0, width * height * 4); }
            finally { _bitmap.UnlockBits(data); }
            var original = Read(x - left, y - top, width);
            for (var i = 1; i <= steps; i++)
            {
                var px = x + dx * i; var py = y + dy * i;
                if (layout.At(px, py) is null) return false;
                if (!ColorChangeSearch.Differs(original, Read(px - left, py - top, width), tolerance)) continue;
                result = (px, py);
                return true;
            }
            return false;
        }
        catch (Exception error) when (error is Win32Exception or ExternalException)
        { Array.Clear(_pixels); return false; }
    }

    private RgbColor Read(int x, int y, int width)
    {
        var i = (y * width + x) * 4;
        return new(_pixels[i + 2], _pixels[i + 1], _pixels[i]);
    }

    private void EnsureBuffers(int width, int height)
    {
        if (_bitmap?.Width == width && _bitmap.Height == height) return;
        _graphics?.Dispose(); _bitmap?.Dispose();
        _bitmap = new(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        _graphics = System.Drawing.Graphics.FromImage(_bitmap);
    }

    public void Dispose() { _graphics?.Dispose(); _bitmap?.Dispose(); _graphics = null; _bitmap = null; Array.Clear(_pixels); }
}
