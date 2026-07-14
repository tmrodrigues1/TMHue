namespace TMHue.Windows.Cursor;

/// <summary>
/// Loads the TMHue eyedropper cursor (.cur) from the app's embedded WPF resources and
/// exposes it as a WPF Cursor. Loading from a pack URI (rather than a loose file next to the
/// exe) keeps this working after single-file publishing, where there is no physical Assets folder.
/// </summary>
public sealed class CursorService
{
    private readonly string _resourceRelativePath;
    private System.Windows.Input.Cursor? _eyedropperCursor;

    public CursorService(string resourceRelativePath)
    {
        _resourceRelativePath = resourceRelativePath;
    }

    /// <summary>
    /// Returns the eyedropper cursor, falling back to the WPF cross cursor if the embedded
    /// resource is ever missing, so the picker never breaks even without the asset.
    /// </summary>
    public System.Windows.Input.Cursor GetEyedropperCursor()
    {
        if (_eyedropperCursor is not null)
            return _eyedropperCursor;

        try
        {
            var uri = new Uri($"pack://application:,,,/{_resourceRelativePath}", UriKind.Absolute);
            var resource = System.Windows.Application.GetResourceStream(uri);
            if (resource is not null)
            {
                _eyedropperCursor = new System.Windows.Input.Cursor(resource.Stream);
                return _eyedropperCursor;
            }
        }
        catch
        {
            // fall through to default below
        }

        return System.Windows.Input.Cursors.Cross;
    }
}
