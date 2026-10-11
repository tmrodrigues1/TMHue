using TMHue.Core.Models;

namespace TMHue.Windows.Display;

public sealed record MonitorArea(string DeviceName, PixelBounds Bounds);

public sealed class MonitorLayout
{
    public MonitorLayout() : this(System.Windows.Forms.Screen.AllScreens.Select(screen =>
        new MonitorArea(screen.DeviceName, new(screen.Bounds.Left, screen.Bounds.Top,
            screen.Bounds.Width, screen.Bounds.Height)))) { }

    public MonitorLayout(IEnumerable<MonitorArea> monitors) => Monitors = monitors.ToArray();

    public IReadOnlyList<MonitorArea> Monitors { get; }
    public MonitorArea? At(int x, int y) => Monitors.FirstOrDefault(m => m.Bounds.Contains(x, y));

    public (int X, int Y) LimitMovement(int x, int y, int dx, int dy, int distance)
    {
        for (var step = 0; step < distance; step++)
        {
            if (At(x + dx, y + dy) is null) break;
            x += dx; y += dy;
        }
        return (x, y);
    }
}
