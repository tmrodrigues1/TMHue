using System.IO;
using Microsoft.Win32;
using TMHue.Windows.Persistence;
using TMHue.Windows.Startup;
using Xunit;

namespace TMHue.IntegrationTests;

// Runs against a throwaway folder and a throwaway registry key that stand in for
// %LocalAppData%\TMHue and for HKCU: the real ones are never touched.
public sealed class UninstallCleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tmhue-uninstall-{Guid.NewGuid():N}");
    private readonly string _hiveName = $@"Software\TMHue.Tests.{Guid.NewGuid():N}";
    private readonly RegistryKey _hive;

    private string DataFolder => Path.Combine(_root, "TMHue");
    private string InstallRoot => Path.Combine(_root, "com.thiagorodrigues.TMHue");

    public UninstallCleanupTests()
    {
        _hive = Registry.CurrentUser.CreateSubKey(_hiveName);
    }

    [Fact]
    public void Run_RemovesEveryFileTheAppWrites()
    {
        var logs = Directory.CreateDirectory(Path.Combine(DataFolder, "Logs")).FullName;
        foreach (var file in new[] { "settings.json", "settings.json.bak", "history.json", "history.json.bak",
                     $"settings.json.{Guid.NewGuid():N}.tmp" })
            File.WriteAllText(Path.Combine(DataFolder, file), "{}");
        foreach (var file in new[] { "errors.log", "errors.1.log", "session.marker" })
            File.WriteAllText(Path.Combine(logs, file), "x");

        UninstallCleanup.Run(DataFolder, _hive, InstallRoot);

        Assert.False(Directory.Exists(DataFolder));
    }

    [Fact]
    public void Run_RemovesReadOnlyFiles()
    {
        Directory.CreateDirectory(DataFolder);
        var file = Path.Combine(DataFolder, "settings.json");
        File.WriteAllText(file, "{}");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        UninstallCleanup.Run(DataFolder, _hive, InstallRoot);

        Assert.False(Directory.Exists(DataFolder));
    }

    [Fact]
    public void Run_RemovesTheFolderOnceALockedFileIsReleased()
    {
        Directory.CreateDirectory(DataFolder);
        var locked = new FileStream(Path.Combine(DataFolder, "errors.log"), FileMode.Create, FileAccess.Write, FileShare.None);
        using var release = new System.Threading.Timer(_ => locked.Dispose(), null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);

        UninstallCleanup.Run(DataFolder, _hive, InstallRoot);

        Assert.False(Directory.Exists(DataFolder));
    }

    [Fact]
    public void Run_RemovesOnlyTheAppsOwnStartupValues()
    {
        using (var run = _hive.CreateSubKey(StartupService.RunKeyPath))
        {
            run.SetValue(StartupService.ValueName, "\"C:\\x\\TMHue.exe\" --minimized");
            run.SetValue("OtherApp", "other.exe");
        }
        using (var approved = _hive.CreateSubKey(UninstallCleanup.StartupApprovedKeyPath))
        {
            approved.SetValue(StartupService.ValueName, new byte[] { 2, 0, 0, 0 }, RegistryValueKind.Binary);
            approved.SetValue("OtherApp", new byte[] { 2, 0, 0, 0 }, RegistryValueKind.Binary);
        }

        UninstallCleanup.Run(DataFolder, _hive, InstallRoot);

        using var runAfter = _hive.OpenSubKey(StartupService.RunKeyPath);
        using var approvedAfter = _hive.OpenSubKey(UninstallCleanup.StartupApprovedKeyPath);
        Assert.Equal(new[] { "OtherApp" }, runAfter?.GetValueNames());
        Assert.Equal(new[] { "OtherApp" }, approvedAfter?.GetValueNames());
    }

    [Fact]
    public void Run_RemovesTrayIconEntriesOfTheInstalledExecutableOnly()
    {
        using (var tray = _hive.CreateSubKey(UninstallCleanup.TrayIconSettingsKeyPath))
        {
            AddTrayEntry(tray, "1", Path.Combine(InstallRoot, "current", "TMHue.exe"));
            AddTrayEntry(tray, "2", Path.Combine(InstallRoot.ToUpperInvariant(), "current", "TMHue.exe"));
            AddTrayEntry(tray, "3", @"C:\Program Files\Other\Other.exe");
            // Same prefix, different folder: must not be mistaken for the installation.
            AddTrayEntry(tray, "4", Path.Combine(InstallRoot + ".Backup", "TMHue.exe"));
            tray.CreateSubKey("5").Dispose(); // entry without ExecutablePath
        }

        UninstallCleanup.Run(DataFolder, _hive, InstallRoot);

        using var after = _hive.OpenSubKey(UninstallCleanup.TrayIconSettingsKeyPath);
        Assert.Equal(new[] { "3", "4", "5" }, after?.GetSubKeyNames().Order());
    }

    [Fact]
    public void Run_WithNothingToRemove_DoesNotThrowOrCreateAnything()
    {
        UninstallCleanup.Run(DataFolder, _hive, installRoot: null);

        Assert.False(Directory.Exists(DataFolder));
        Assert.Empty(_hive.GetSubKeyNames());
    }

    [Fact]
    public void DeleteCrashDumps_RemovesOnlyThisExecutablesDumps()
    {
        var dumps = Directory.CreateDirectory(Path.Combine(_root, "CrashDumps")).FullName;
        foreach (var file in new[] { "TMHue.exe.1234.dmp", "TMHue.exe.99.dmp", "Other.exe.1.dmp", "TMHue.exe.notes.txt" })
            File.WriteAllText(Path.Combine(dumps, file), "x");

        UninstallCleanup.DeleteCrashDumps(dumps, "TMHue.exe");

        Assert.Equal(new[] { "Other.exe.1.dmp", "TMHue.exe.notes.txt" },
            Directory.GetFiles(dumps).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void ScheduleDeletionAfterExit_WaitsForTheProcessAndThenRemovesFilesAndFolder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "velopack")).FullName;
        var ownLog = Path.Combine(folder, "velopack_app.log");
        var sharedLog = Path.Combine(folder, "velopack.log");
        File.WriteAllText(ownLog, "x");
        File.WriteAllText(sharedLog, "x");
        // Stands in for the uninstaller: alive for about six seconds.
        using var running = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "PING.EXE"),
            Arguments = "-n 7 127.0.0.1",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.NotNull(running);

        UninstallCleanup.ScheduleDeletionAfterExit(running.Id, "PING.EXE", new[] { ownLog, sharedLog }, folder);

        Thread.Sleep(TimeSpan.FromSeconds(3));
        Assert.False(running.HasExited);
        Assert.True(File.Exists(ownLog), "the log was deleted while the process was still running");

        running.WaitForExit();
        WaitUntil(() => !Directory.Exists(folder), TimeSpan.FromSeconds(20));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void ScheduleDeletionAfterExit_KeepsAFolderOtherApplicationsStillUse()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "velopack")).FullName;
        var ownLog = Path.Combine(folder, "velopack_app.log");
        var otherLog = Path.Combine(folder, "velopack_other.log");
        File.WriteAllText(ownLog, "x");
        File.WriteAllText(otherLog, "x");

        UninstallCleanup.ScheduleDeletionAfterExit(0, "Update.exe", new[] { ownLog }, folder);

        WaitUntil(() => !File.Exists(ownLog), TimeSpan.FromSeconds(20));
        Assert.False(File.Exists(ownLog));
        Assert.True(File.Exists(otherLog));
    }

    [Fact]
    public void ScheduleDeletionAfterExit_RefusesPathsTheShellCouldReinterpret()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "velopack")).FullName;
        var log = Path.Combine(folder, "velopack_app.log");
        File.WriteAllText(log, "x");

        UninstallCleanup.ScheduleDeletionAfterExit(0, "Update.exe", new[] { log, folder + "\a & calc" }, folder);

        Thread.Sleep(TimeSpan.FromSeconds(5));
        Assert.True(File.Exists(log));
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline) Thread.Sleep(250);
    }

    private static void AddTrayEntry(RegistryKey tray, string name, string executable)
    {
        using var entry = tray.CreateSubKey(name);
        entry.SetValue("ExecutablePath", executable);
    }

    public void Dispose()
    {
        _hive.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_hiveName, throwOnMissingSubKey: false);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
