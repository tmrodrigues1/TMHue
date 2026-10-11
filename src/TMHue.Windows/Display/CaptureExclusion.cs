using System.Runtime.InteropServices;
using TMHue.Windows.Native;

namespace TMHue.Windows.Display;

public readonly record struct CaptureExclusionResult(bool Succeeded, int NativeError);

public static class CaptureExclusion
{
    public static CaptureExclusionResult Apply(nint window)
    {
        // Older Windows treats 0x11 as WDA_MONITOR (a black rectangle), not exclusion.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            return new(false, 50); // ERROR_NOT_SUPPORTED
        if (!NativeMethods.SetWindowDisplayAffinity(window, NativeMethods.WDA_EXCLUDEFROMCAPTURE))
            return new(false, Marshal.GetLastPInvokeError());
        if (!NativeMethods.GetWindowDisplayAffinity(window, out var affinity))
            return new(false, Marshal.GetLastPInvokeError());
        return new(affinity == NativeMethods.WDA_EXCLUDEFROMCAPTURE, 0);
    }
}
