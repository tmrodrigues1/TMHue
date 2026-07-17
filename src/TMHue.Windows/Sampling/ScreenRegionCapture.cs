using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using TMHue.Windows.Native;

namespace TMHue.Windows.Sampling;

/// <summary>Captures a physical screen region, scaling it during the GDI copy when necessary so
/// an oversized selection never creates a full-resolution bitmap in process memory.</summary>
public static class ScreenRegionCapture
{
    public const int MaxCaptureDimension = 2048;

    /// <summary>Attempts a capture without surfacing expected GDI failures from protected or
    /// transiently unavailable screen regions to the palette-selection flow.</summary>
    public static bool TryCapture(int x, int y, int width, int height, out BitmapSource? capture)
    {
        try
        {
            capture = Capture(x, y, width, height);
            return true;
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            capture = null;
            return false;
        }
    }

    public static BitmapSource Capture(int x, int y, int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var (targetWidth, targetHeight) = GetTargetDimensions(width, height);

        using var bitmap = new System.Drawing.Bitmap(targetWidth, targetHeight,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var targetGraphics = System.Drawing.Graphics.FromImage(bitmap);

        nint targetDc = nint.Zero;
        nint sourceDc = nint.Zero;
        try
        {
            targetDc = targetGraphics.GetHdc();
            sourceDc = NativeMethods.GetDC(nint.Zero);
            if (sourceDc == nint.Zero)
                throw new InvalidOperationException("Unable to access the screen device context.");

            if (!NativeMethods.StretchBlt(
                    targetDc, 0, 0, targetWidth, targetHeight,
                    sourceDc, x, y, width, height,
                    NativeMethods.SRCCOPY))
            {
                throw new System.ComponentModel.Win32Exception();
            }
        }
        finally
        {
            if (sourceDc != nint.Zero)
                NativeMethods.ReleaseDC(nint.Zero, sourceDc);
            if (targetDc != nint.Zero)
                targetGraphics.ReleaseHdc(targetDc);
        }

        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, nint.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    private static (int Width, int Height) GetTargetDimensions(int width, int height)
    {
        var scale = Math.Min(1d, MaxCaptureDimension / (double)Math.Max(width, height));
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }
}
