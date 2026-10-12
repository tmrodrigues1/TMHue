using System.ComponentModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.Services;
using TMHue.Core.ValueObjects;
using TMHue.Windows.Display;
using TMHue.Windows.Native;

namespace TMHue.Windows.Sampling;

/// <summary>One monitor's snapshot. The screen is copied once, into a shared-memory section
/// that backs both the displayed <see cref="Image"/> and every pixel read: 4 bytes per pixel,
/// outside the managed heap, wiped on disposal.</summary>
public sealed unsafe class FrozenMonitorFrame : IDisposable
{
    private readonly MemoryMappedFile _section;
    private readonly nint _byteCount;
    private nint _bitmap;
    private byte* _pixels;

    private FrozenMonitorFrame(MonitorArea monitor, MemoryMappedFile section, nint bitmap, byte* pixels,
        nint byteCount, BitmapSource image)
    {
        Monitor = monitor; _section = section; _bitmap = bitmap; _pixels = pixels; _byteCount = byteCount;
        Image = image;
    }

    public MonitorArea Monitor { get; }
    public BitmapSource Image { get; }

    internal static FrozenMonitorFrame Capture(MonitorArea monitor)
    {
        var bounds = monitor.Bounds;
        var byteCount = checked(bounds.Width * bounds.Height * 4);
        var section = MemoryMappedFile.CreateNew(null, byteCount, MemoryMappedFileAccess.ReadWrite);
        var bitmap = nint.Zero;
        try
        {
            var sectionHandle = section.SafeMemoryMappedFileHandle.DangerousGetHandle();
            var header = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(NativeMethods.BITMAPINFOHEADER),
                biWidth = bounds.Width,
                biHeight = -bounds.Height, // top-down, matching the image's row order
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB
            };
            bitmap = NativeMethods.CreateDIBSection(nint.Zero, in header, NativeMethods.DIB_RGB_COLORS,
                out var pixels, sectionHandle, 0);
            if (bitmap == nint.Zero || pixels == nint.Zero)
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            CopyScreen(bitmap, bounds);
            var image = Imaging.CreateBitmapSourceFromMemorySection(sectionHandle, bounds.Width, bounds.Height,
                PixelFormats.Bgr32, bounds.Width * 4, 0);
            image.Freeze();
            return new(monitor, section, bitmap, (byte*)pixels, byteCount, image);
        }
        catch
        {
            if (bitmap != nint.Zero) NativeMethods.DeleteObject(bitmap);
            section.Dispose();
            throw;
        }
    }

    private static void CopyScreen(nint bitmap, PixelBounds bounds)
    {
        var screen = NativeMethods.GetDC(nint.Zero);
        if (screen == nint.Zero) throw new Win32Exception("Unable to access the screen device context.");
        var memory = nint.Zero;
        var previous = nint.Zero;
        try
        {
            memory = NativeMethods.CreateCompatibleDC(screen);
            if (memory == nint.Zero) throw new Win32Exception("Unable to create a memory device context.");
            previous = NativeMethods.SelectObject(memory, bitmap);
            if (!NativeMethods.BitBlt(memory, 0, 0, bounds.Width, bounds.Height,
                    screen, bounds.Left, bounds.Top, NativeMethods.SRCCOPY))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        finally
        {
            if (previous != nint.Zero) NativeMethods.SelectObject(memory, previous);
            if (memory != nint.Zero) NativeMethods.DeleteDC(memory);
            NativeMethods.ReleaseDC(nint.Zero, screen);
        }
    }

    internal bool TryReadPixel(int x, int y, out RgbColor color)
    {
        color = default;
        var bounds = Monitor.Bounds;
        if (_pixels is null || !bounds.Contains(x, y)) return false;
        var pixel = _pixels + ((nint)(y - bounds.Top) * bounds.Width + (x - bounds.Left)) * 4;
        color = new(pixel[2], pixel[1], pixel[0]);
        return true;
    }

    /// <summary>Wipes the pixels through the shared section before releasing it, so the copy
    /// still mapped by <see cref="Image"/> holds no screen content while it awaits collection.</summary>
    public void Dispose()
    {
        if (_pixels is null) return;
        NativeMemory.Clear(_pixels, (nuint)_byteCount);
        _pixels = null;
        NativeMethods.DeleteObject(_bitmap);
        _bitmap = nint.Zero;
        _section.Dispose();
    }
}

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
        var bytesNeeded = layout.Monitors.Sum(m => (long)m.Bounds.Width * m.Bounds.Height * 4);
        if (bytesNeeded <= 0 || bytesNeeded > MaxMemoryBytes) return false;
        var result = new FrozenScreenSampler();
        var frames = new List<FrozenMonitorFrame>();
        try
        {
            foreach (var monitor in layout.Monitors)
            {
                cancellation.ThrowIfCancellationRequested();
                frames.Add(FrozenMonitorFrame.Capture(monitor));
            }
            cancellation.ThrowIfCancellationRequested();
            result.Frames = frames;
            sampler = result;
            return true;
        }
        catch (OperationCanceledException)
        {
            foreach (var frame in frames) frame.Dispose();
            result.Dispose();
            throw;
        }
        catch (Exception error) when (error is Win32Exception or ExternalException or OutOfMemoryException or IOException)
        {
            foreach (var frame in frames) frame.Dispose();
            result.Dispose();
            return false;
        }
    }

    public bool TryReadPixel(int x, int y, out RgbColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        color = default;
        foreach (var frame in Frames)
            if (frame.TryReadPixel(x, y, out color)) return true;
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
        foreach (var frame in Frames) frame.Dispose();
        Frames = Array.Empty<FrozenMonitorFrame>();
        _sample?.Clear(); _sample = null;
    }
}
