using Microsoft.Win32;
using TMHue.Core.Interfaces;

namespace TMHue.Windows.Startup;

/// <summary>
/// Controls Windows sign-in launch via the per-user Run registry key. When TMHue is later
/// packaged as MSIX, this can be swapped for a StartupTask without changing the interface.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TMHue";

    private readonly string _executablePath;

    public StartupService(string executablePath)
    {
        _executablePath = executablePath;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string existing &&
                   existing.Equals($"\"{_executablePath}\" --minimized", StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
            key.SetValue(ValueName, $"\"{_executablePath}\" --minimized");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
