using TMHue.App.Views;
using TMHue.Windows.Display;
using TMHue.Windows.Sampling;

namespace TMHue.App.Infrastructure;

/// <summary>One HWND per monitor avoids virtual-desktop DIP/physical mismatches with mixed DPI.</summary>
internal sealed class PickerOverlayHost : IDisposable
{
    public List<PickerOverlayWindow> Windows { get; } = new();

    public PickerOverlayHost(MonitorLayout layout, FrozenScreenSampler? snapshot, System.Windows.Input.Cursor cursor)
    {
        foreach (var monitor in layout.Monitors)
        {
            var window = new PickerOverlayWindow(monitor.Bounds) { Cursor = cursor, ShowActivated = false };
            var frame = snapshot?.Frames.FirstOrDefault(f => f.Monitor.DeviceName == monitor.DeviceName);
            if (frame is not null) window.SetFrozenImage(frame.Image);
            Windows.Add(window);
        }
    }

    public bool Show(int x, int y, MonitorLayout layout)
    {
        foreach (var window in Windows)
        {
            window.Show();
            if (!window.CaptureExclusionStatus.Succeeded) return false;
        }
        var index = layout.Monitors.ToList().FindIndex(m => m.Bounds.Contains(x, y));
        var active = Windows[Math.Max(0, index)];
        active.Activate(); active.Focus();
        System.Windows.Input.Keyboard.Focus(active);
        return true;
    }

    public void Dispose()
    {
        foreach (var window in Windows) window.Close();
        Windows.Clear();
    }
}
