namespace TMHue.Core.Interfaces;

public interface INotificationService
{
    /// <param name="hex">Used for the swatch color preview, always a valid hex string.</param>
    /// <param name="displayText">Text shown in the toast; defaults to <paramref name="hex"/> when null,
    /// but can differ when the copied format isn't hex (e.g. rgb(...)/hsl(...)).</param>
    void ShowCaptureConfirmation(string hex, string? displayText = null);
}
