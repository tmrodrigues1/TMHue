using TMHue.Core.Interfaces;

namespace TMHue.Windows.Clipboard;

/// <summary>Wraps WPF's Clipboard API with retries, since another process can transiently own the clipboard.</summary>
public sealed class ClipboardService : IClipboardService
{
    public bool TrySetText(string text, int maxAttempts = 3)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                if (attempt == maxAttempts)
                    return false;

                Thread.Sleep(40 * attempt);
            }
        }

        return false;
    }
}
