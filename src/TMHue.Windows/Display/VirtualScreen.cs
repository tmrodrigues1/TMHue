using TMHue.Windows.Native;

namespace TMHue.Windows.Display;

/// <summary>Bounds of the virtual desktop spanning every monitor, including negative-coordinate monitors.</summary>
public readonly record struct VirtualScreenBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;

    public static VirtualScreenBounds GetCurrent()
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        return new VirtualScreenBounds(left, top, width, height);
    }
}
