namespace TMHue.Core.Models;

public enum CaptureColorOutput
{
    /// <summary>Unmodified 8-bit GDI values of rendered content; not a guarantee of sRGB/HDR fidelity.</summary>
    Rendered,
    /// <summary>SDR device RGB converted through the monitor ICC profile to sRGB.</summary>
    Srgb
}
