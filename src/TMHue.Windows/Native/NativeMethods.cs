using System.Runtime.InteropServices;

namespace TMHue.Windows.Native;

/// <summary>Raw Win32 interop surface. Kept minimal and internal-facing so higher layers never see COORD/HWND plumbing.</summary>
internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPhysicalCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hDC);

    [LibraryImport("gdi32.dll")]
    public static partial uint GetPixel(nint hDC, int x, int y);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool StretchBlt(
        nint hdcDest, int xDest, int yDest, int widthDest, int heightDest,
        nint hdcSource, int xSource, int ySource, int widthSource, int heightSource,
        uint rasterOperation);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint hObject);

    public const uint SRCCOPY = 0x00CC0020;

    public static (byte R, byte G, byte B) ColorRefToRgb(uint colorRef) =>
        ((byte)(colorRef & 0x000000FF), (byte)((colorRef & 0x0000FF00) >> 8), (byte)((colorRef & 0x00FF0000) >> 16));

    public const uint CR_INVALID = 0xFFFFFFFF;

    // --- Virtual screen metrics ---
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int index);

    // --- Global hotkeys ---
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hWnd, int id);

    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // --- Capture affinity ---
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    public const uint WDA_NONE = 0x0;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    // --- Extended window styles (used to make the magnifier lens click-through/non-activating) ---
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    public static partial int GetWindowLongW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    public static partial int SetWindowLongW(nint hWnd, int nIndex, int dwNewLong);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // --- Window positioning (used to place overlays in physical pixels, bypassing WPF DIP scaling) ---
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    public static readonly nint HWND_TOPMOST = new(-1);
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOMOVE = 0x0002;
}
