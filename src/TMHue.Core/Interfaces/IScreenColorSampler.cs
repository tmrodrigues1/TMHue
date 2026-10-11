using TMHue.Core.ValueObjects;
using TMHue.Core.Models;

namespace TMHue.Core.Interfaces;

public interface IScreenColorSampler
{
    /// <summary>Reads the pixel color at the given physical screen coordinates.</summary>
    bool TryReadPixel(int physicalX, int physicalY, out RgbColor color);

    /// <summary>Reads a square (odd side, 1..11), clipped to actual monitors. False returns
    /// no sample; invalid cells are excluded from selection. Consume reused buffers before
    /// the next read and call only from the UI thread.</summary>
    bool TryReadRegion(int centerX, int centerY, int side, out ScreenSample? sample);

    /// <summary>Walks from (startX, startY) in steps of (stepX, stepY) — one of which must be
    /// 0 and the other +/-1 — looking for the first pixel whose color differs from the start
    /// pixel, stopping after at most <paramref name="maxSteps"/> steps. Used for the eyedropper's
    /// Shift+Arrow navigation. Implementations use a bounded strip capture or a snapshot,
    /// avoiding one synchronous driver round trip per scanned pixel.</summary>
    bool TryFindNextColorChange(int startX, int startY, int stepX, int stepY, int maxSteps, out (int X, int Y) result);

    bool TryFindNextColorChange(int startX, int startY, int stepX, int stepY, int maxSteps,
        int tolerance, out (int X, int Y) result) =>
        TryFindNextColorChange(startX, startY, stepX, stepY, maxSteps, out result);
}
