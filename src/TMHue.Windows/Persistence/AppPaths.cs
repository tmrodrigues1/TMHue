using System.IO;

namespace TMHue.Windows.Persistence;

/// <summary>Central source of truth for every on-disk location TMHue touches.</summary>
public static class AppPaths
{
    private const string FolderName = "TMHue";

    public static string RootFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

    public static string SettingsFile => Path.Combine(RootFolder, "settings.json");

    public static string HistoryFile => Path.Combine(RootFolder, "history.json");

    public static string LogsFolder => Path.Combine(RootFolder, "Logs");

    /// <summary>
    /// Pack-URI-relative path (not a filesystem path). The cursor ships as a WPF "Resource" build item
    /// embedded in the entry assembly, so it survives single-file publishing; it is never a loose file on disk.
    /// </summary>
    public const string CursorResourcePath = "Assets/eyedropper.cur";
}
