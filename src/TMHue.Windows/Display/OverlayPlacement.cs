using TMHue.Windows.Native;

namespace TMHue.Windows.Display;

/// <summary>Positions a native window in physical pixels, sidestepping WPF's per-window DIP scaling across mixed-DPI monitors.</summary>
public static class OverlayPlacement
{
    /// <summary>Re-asserts HWND_TOPMOST without touching position or size, which WPF's own Left/Top/Width/Height already own.</summary>
    public static void ForceTopmost(nint hwnd)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOMOVE | SWP_NOSIZE);
    }

    public static void PlaceAt(nint hwnd, int x, int y)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            x, y, 0, 0,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW | SWP_NOSIZE);
    }

    private const uint SWP_NOSIZE = 0x0001;

    public static CaptureExclusionResult ExcludeFromCapture(nint hwnd) => CaptureExclusion.Apply(hwnd);

    /// <summary>Marks a window click-through and non-activating, so it can float on top of
    /// another window (e.g. the picker overlay) without stealing its mouse capture or focus.</summary>
    public static void MakeClickThroughAndNonActivating(nint hwnd)
    {
        var exStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE);
    }

    public static (int X, int Y) GetPhysicalCursorPosition()
    {
        if (!NativeMethods.GetPhysicalCursorPos(out var point))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());
        return (point.X, point.Y);
    }

    /// <summary>Moves the system cursor in physical pixels. Windows delivers this exactly like a
    /// hardware move (WM_MOUSEMOVE to whatever's under the cursor), so the existing pointer-moved
    /// pipeline picks it up without any extra plumbing.</summary>
    public static void SetCursorPosition(int x, int y) => NativeMethods.SetCursorPos(x, y);

    public static void PlaceMonitorWindow(nint window, TMHue.Core.Models.PixelBounds bounds) =>
        NativeMethods.SetWindowPos(window, NativeMethods.HWND_TOPMOST, bounds.Left, bounds.Top,
            bounds.Width, bounds.Height, NativeMethods.SWP_NOACTIVATE);

    public static double GetWindowScale(nint window) => Math.Max(96, NativeMethods.GetDpiForWindow(window)) / 96d;
}
