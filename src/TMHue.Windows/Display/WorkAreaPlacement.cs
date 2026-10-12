using TMHue.Core.Models;

namespace TMHue.Windows.Display;

/// <summary>Decides, in physical pixels, whether a window is still within reach on the monitors
/// attached right now. A saved position can point at a monitor that was since unplugged.</summary>
public static class WorkAreaPlacement
{
    // Enough of the title area to grab and drag the window back.
    private const int MinReachableWidth = 64;
    private const int MinReachableHeight = 24;

    public static IReadOnlyList<PixelBounds> CurrentWorkAreas() =>
        System.Windows.Forms.Screen.AllScreens
            .Select(screen => ToBounds(screen.WorkingArea))
            .ToArray();

    public static PixelBounds PrimaryWorkArea() =>
        ToBounds((System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0]).WorkingArea);

    /// <summary>True when a draggable portion of <paramref name="titleArea"/> lies inside one
    /// of <paramref name="workAreas"/>.</summary>
    public static bool IsReachable(PixelBounds titleArea, IReadOnlyList<PixelBounds> workAreas)
    {
        foreach (var area in workAreas)
        {
            var width = Math.Min(titleArea.Right, area.Right) - Math.Max(titleArea.Left, area.Left);
            var height = Math.Min(titleArea.Bottom, area.Bottom) - Math.Max(titleArea.Top, area.Top);
            if (width >= Math.Min(MinReachableWidth, titleArea.Width)
                && height >= Math.Min(MinReachableHeight, titleArea.Height))
                return true;
        }

        return false;
    }

    /// <summary>Top-left corner that centers a window of the given size in <paramref name="workArea"/>.</summary>
    public static (int X, int Y) Center(PixelBounds workArea, int width, int height) =>
        (workArea.Left + Math.Max(0, (workArea.Width - width) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - height) / 2));

    private static PixelBounds ToBounds(System.Drawing.Rectangle rectangle) =>
        new(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);
}
