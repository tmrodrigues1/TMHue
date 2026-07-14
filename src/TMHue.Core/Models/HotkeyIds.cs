namespace TMHue.Core.Models;

/// <summary>Well-known identifiers for the app's global hotkeys, used to tell them apart in
/// <see cref="TMHue.Core.Interfaces.IGlobalHotkeyService.HotkeyPressed"/>.</summary>
public static class HotkeyIds
{
    public const string Capture = "capture";
    public const string OpenApp = "open-app";
    public const string OpenContrastChecker = "open-contrast-checker";

    /// <summary>Bare Esc, registered only for the duration of a picking session so cancel works
    /// even if the overlay loses keyboard focus.</summary>
    public const string CancelCapture = "cancel-capture";
}
