namespace TMHue.Core.Interfaces;

public interface IClipboardService
{
    /// <summary>Writes text to the Windows clipboard, retrying briefly if another process holds it.</summary>
    bool TrySetText(string text, int maxAttempts = 3);
}
