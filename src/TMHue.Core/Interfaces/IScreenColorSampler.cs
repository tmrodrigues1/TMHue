using TMHue.Core.ValueObjects;

namespace TMHue.Core.Interfaces;

public interface IScreenColorSampler
{
    /// <summary>Reads the pixel color at the given physical screen coordinates.</summary>
    bool TryReadPixel(int physicalX, int physicalY, out RgbColor color);

    /// <summary>Reads a square region (side x side) centered on the given point, for magnifier
    /// rendering and area sampling. The returned matrix may be a buffer reused across calls
    /// (this runs on every mouse move during a capture): consume it before calling again, and
    /// only call from the UI thread's input pipeline.</summary>
    RgbColor[,] ReadRegion(int centerX, int centerY, int side);

    /// <summary>Walks from (startX, startY) in steps of (stepX, stepY) — one of which must be
    /// 0 and the other +/-1 — looking for the first pixel whose color differs from the start
    /// pixel, stopping after at most <paramref name="maxSteps"/> steps. Used for the eyedropper's
    /// Shift+Arrow "jump to next color change" navigation. Holds a single device context for the
    /// whole walk instead of one per pixel, since that per-pixel GetDC/ReleaseDC pair is what
    /// made a naive pixel-by-pixel scan freeze the UI thread for whole-screen solid-color runs.</summary>
    bool TryFindNextColorChange(int startX, int startY, int stepX, int stepY, int maxSteps, out (int X, int Y) result);
}
