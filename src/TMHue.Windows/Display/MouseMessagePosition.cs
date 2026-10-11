using TMHue.Windows.Native;

namespace TMHue.Windows.Display;

public static class MouseMessagePosition
{
    /// <summary>Decode signed client coordinates carried by WM_LBUTTONDOWN. Reading the
    /// current cursor instead can select a later position when input has been queued.</summary>
    public static (int X, int Y) Decode(nint lParam) =>
        (unchecked((short)(long)lParam), unchecked((short)((long)lParam >> 16)));

    public static bool TryGetScreenPosition(nint window, nint lParam, out (int X, int Y) position)
    {
        var (x, y) = Decode(lParam);
        var point = new NativeMethods.POINT { X = x, Y = y };
        position = default;
        if (!NativeMethods.ClientToScreen(window, ref point)) return false;
        position = (point.X, point.Y);
        return true;
    }
}
