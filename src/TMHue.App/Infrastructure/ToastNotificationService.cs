using TMHue.App.Views;
using TMHue.Core.Interfaces;

namespace TMHue.App.Infrastructure;

/// <summary>
/// Shows the capture confirmation as a centered, topmost toast that overlays any application —
/// unlike a tray balloon, which is corner-anchored and easy to miss.
/// </summary>
public sealed class ToastNotificationService : INotificationService
{
    public void ShowCaptureConfirmation(string hex, string? displayText = null)
    {
        var toast = new CaptureToastWindow(hex, displayText ?? hex);
        toast.Show();
    }
}
