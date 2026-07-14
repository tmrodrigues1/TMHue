using TMHue.Core.Interfaces;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Native;

namespace TMHue.Windows.Sampling;

/// <summary>
/// Reads individual pixels via GDI (GetDC + GetPixel). Deliberately avoids full-screen capture:
/// the app only ever needs a handful of pixels around the cursor, never a frame buffer.
/// </summary>
public sealed class ScreenColorSampler : IScreenColorSampler
{
    public bool TryReadPixel(int physicalX, int physicalY, out RgbColor color)
    {
        var hdc = NativeMethods.GetDC(0);
        if (hdc == 0)
        {
            color = default;
            return false;
        }

        try
        {
            var colorRef = NativeMethods.GetPixel(hdc, physicalX, physicalY);
            if (colorRef == NativeMethods.CR_INVALID)
            {
                color = default;
                return false;
            }

            var (r, g, b) = NativeMethods.ColorRefToRgb(colorRef);
            color = new RgbColor(r, g, b);
            return true;
        }
        finally
        {
            NativeMethods.ReleaseDC(0, hdc);
        }
    }

    public bool TryFindNextColorChange(int startX, int startY, int stepX, int stepY, int maxSteps, out (int X, int Y) result)
    {
        result = default;

        var hdc = NativeMethods.GetDC(0);
        if (hdc == 0) return false;

        try
        {
            var startRef = NativeMethods.GetPixel(hdc, startX, startY);
            if (startRef == NativeMethods.CR_INVALID) return false;

            var x = startX;
            var y = startY;

            for (var i = 0; i < maxSteps; i++)
            {
                x += stepX;
                y += stepY;

                var colorRef = NativeMethods.GetPixel(hdc, x, y);
                if (colorRef == NativeMethods.CR_INVALID) return false;

                if (colorRef != startRef)
                {
                    result = (x, y);
                    return true;
                }
            }

            return false;
        }
        finally
        {
            NativeMethods.ReleaseDC(0, hdc);
        }
    }

    // ReadRegion runs on every mouse move during a capture, so the bitmap, the pixel byte
    // buffer and the returned matrix are all reused between calls (recreated only when the
    // requested side changes). Callers must consume the returned matrix before the next call;
    // the sampler is only ever used from the UI thread's mouse-move pipeline.
    private System.Drawing.Bitmap? _regionBitmap;
    private System.Drawing.Graphics? _regionGraphics;
    private byte[]? _regionPixels;
    private RgbColor[,]? _region;
    private int _regionSide;

    /// <summary>Grabs the whole region with a single BitBlt (CopyFromScreen) instead of one
    /// GetPixel round-trip per pixel: each GetPixel is a synchronous driver call, and side²
    /// of them per mouse-move made the magnifier visibly stutter.</summary>
    public RgbColor[,] ReadRegion(int centerX, int centerY, int side)
    {
        var half = side / 2;
        EnsureRegionBuffers(side);
        var result = _region!;

        try
        {
            _regionGraphics!.CopyFromScreen(centerX - half, centerY - half, 0, 0, new System.Drawing.Size(side, side));

            var data = _regionBitmap!.LockBits(
                new System.Drawing.Rectangle(0, 0, side, side),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var pixels = _regionPixels!;
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, data.Stride * side);

                for (var row = 0; row < side; row++)
                {
                    var rowStart = row * data.Stride;
                    for (var col = 0; col < side; col++)
                    {
                        var i = rowStart + col * 4; // BGRA
                        result[row, col] = new RgbColor(pixels[i + 2], pixels[i + 1], pixels[i]);
                    }
                }
            }
            finally
            {
                _regionBitmap.UnlockBits(data);
            }
        }
        catch
        {
            // Off-screen/secure regions: leave the cells as they are (black on first use,
            // the previous frame's pixels afterwards — the buffer is reused between calls).
        }

        return result;
    }

    private void EnsureRegionBuffers(int side)
    {
        if (_regionSide == side && _regionBitmap is not null) return;

        _regionGraphics?.Dispose();
        _regionBitmap?.Dispose();

        _regionBitmap = new System.Drawing.Bitmap(side, side, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        _regionGraphics = System.Drawing.Graphics.FromImage(_regionBitmap);
        // Stride is rounded up to a 4-byte boundary, which 32bpp already satisfies; side * 4
        // therefore matches the stride LockBits will report for this bitmap.
        _regionPixels = new byte[side * 4 * side];
        _region = new RgbColor[side, side];
        _regionSide = side;
    }
}
