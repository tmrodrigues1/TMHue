using System.IO;
using Microsoft.Win32;
using TMHue.Windows.Startup;

namespace TMHue.Windows.Persistence;

/// <summary>
/// Removes everything TMHue leaves on the machine outside its installation folder. The Velopack
/// uninstaller only knows about what it created itself (installation folder, shortcuts and the
/// "installed apps" entry); settings, history, logs and the per-user registry values written or
/// caused by the app would otherwise survive the uninstall.
/// Best-effort by design: no failure here may ever abort the uninstall.
/// </summary>
public static class UninstallCleanup
{
    // Written by Windows (Task Manager / Settings > Startup) when the user toggles the Run entry.
    internal const string StartupApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    // Windows 11 keeps one subkey per executable that ever showed a tray icon.
    internal const string TrayIconSettingsKeyPath = @"Control Panel\NotifyIconSettings";

    private const int DeleteAttempts = 10;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(200);

    /// <param name="installRoot">Installation root; tray-icon entries of executables under it
    /// are removed. Null skips that step.</param>
    /// <param name="packageId">Velopack package id, which names its per-app log file.</param>
    public static void Run(string? installRoot, string packageId)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Run(AppPaths.RootFolder, Registry.CurrentUser, installRoot);
        Attempt(() => DeleteCrashDumps(Path.Combine(localAppData, "CrashDumps"), "TMHue.exe"));
        if (!string.IsNullOrWhiteSpace(installRoot))
        {
            Attempt(() => ScheduleUpdaterLogRemoval(
                Path.Combine(localAppData, "velopack"), packageId, Path.Combine(installRoot, "Update.exe")));
        }
    }

    internal static void Run(string dataFolder, RegistryKey userHive, string? installRoot)
    {
        Attempt(() => DeleteFolder(dataFolder));
        Attempt(() => DeleteValue(userHive, StartupService.RunKeyPath, StartupService.ValueName));
        Attempt(() => DeleteValue(userHive, StartupApprovedKeyPath, StartupService.ValueName));
        if (!string.IsNullOrWhiteSpace(installRoot))
            Attempt(() => DeleteTrayIconEntries(userHive, installRoot));
    }

    /// <summary>Dumps Windows writes for this executable when local crash dumps are enabled.</summary>
    internal static void DeleteCrashDumps(string crashDumpsFolder, string executableName)
    {
        if (!Directory.Exists(crashDumpsFolder)) return;

        foreach (var dump in Directory.EnumerateFiles(crashDumpsFolder, executableName + ".*.dmp"))
            File.Delete(dump);
    }

    /// <summary>Velopack logs outside the installation folder, and both the uninstaller and the
    /// library inside this very process keep writing there until they exit — so the log can only
    /// go away after the uninstaller is gone. The shared "velopack.log" and the folder itself are
    /// removed only when no other Velopack application uses them.</summary>
    private static void ScheduleUpdaterLogRemoval(string logFolder, string packageId, string updaterPath)
    {
        if (!Directory.Exists(logFolder)) return;

        var ownLog = Path.Combine(logFolder, $"velopack_{packageId}.log");
        var files = new List<string> { ownLog };
        var otherApps = Directory.EnumerateFiles(logFolder, "velopack_*.log")
            .Any(log => !log.Equals(ownLog, StringComparison.OrdinalIgnoreCase));
        if (!otherApps) files.Add(Path.Combine(logFolder, "velopack.log"));

        using var uninstaller = System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(updaterPath))
            .FirstOrDefault(process => IsRunningFrom(process, updaterPath));
        ScheduleDeletionAfterExit(uninstaller?.Id ?? 0, Path.GetFileName(updaterPath), files, logFolder);
    }

    private static bool IsRunningFrom(System.Diagnostics.Process process, string executablePath)
    {
        try
        {
            return string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false; // exited meanwhile, or not ours to inspect
        }
    }

    /// <summary>Starts a detached, windowless command that waits for a process to exit and then
    /// deletes <paramref name="files"/> and, if it ended up empty, <paramref name="folder"/>.
    /// The same technique the Velopack uninstaller uses to delete its own executable: nothing
    /// that is still running can remove files it is still writing.</summary>
    internal static void ScheduleDeletionAfterExit(int processId, string imageName, IReadOnlyList<string> files, string folder)
    {
        // The paths go into a command line; anything cmd.exe could reinterpret is refused.
        if (files.Append(folder).Append(imageName).Any(text => text.IndexOfAny(['"', '%', '^', '!', '&', '|', '<', '>']) >= 0))
            return;

        // Full paths: resolved by name, another find.exe earlier in PATH (Git ships one) would
        // run instead, and nothing here may depend on what PATH happens to contain.
        var tasklist = Path.Combine(Environment.SystemDirectory, "tasklist.exe");
        var find = Path.Combine(Environment.SystemDirectory, "find.exe");
        var ping = Path.Combine(Environment.SystemDirectory, "ping.exe");

        var deletions = string.Join(" & ", files.Select(file => $"del /f /q \"{file}\" 2>nul"));
        // Polls about once a second, for up to ~10 minutes (the uninstaller may sit on its final
        // dialog); once the process is gone, waits two more seconds for handles to be released.
        var command =
            $"for /l %i in (1,1,600) do (\"{tasklist}\" /fi \"PID eq {processId}\" /fi \"IMAGENAME eq {imageName}\" /nh 2>nul"
            + $" | \"{find}\" /i \"{imageName}\" >nul && (\"{ping}\" -n 2 127.0.0.1 >nul)"
            + $" || (\"{ping}\" -n 3 127.0.0.1 >nul & {deletions} & rd \"{folder}\" 2>nul & exit))";

        using var helper = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/d /s /c \"{command}\"",
            // Not the installation folder: a process sitting in it would block its removal.
            WorkingDirectory = Environment.SystemDirectory,
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    /// <summary>Settings, history, logs, backups and the session marker. Retries because an
    /// antivirus or indexer may hold a file for a moment, and clears read-only attributes,
    /// which would otherwise block the deletion.</summary>
    private static void DeleteFolder(string folder)
    {
        // Never recurse from anything but a full path to the app's own folder.
        if (!Path.IsPathFullyQualified(folder)) return;

        for (var attempt = 1; attempt <= DeleteAttempts && Directory.Exists(folder); attempt++)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // something still holds a file; the next attempt may find it released
            }

            if (Directory.Exists(folder)) Thread.Sleep(DeleteRetryDelay);
        }
    }

    private static void DeleteValue(RegistryKey hive, string keyPath, string valueName)
    {
        using var key = hive.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static void DeleteTrayIconEntries(RegistryKey hive, string installRoot)
    {
        using var settings = hive.OpenSubKey(TrayIconSettingsKeyPath, writable: true);
        if (settings is null) return;

        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot)) + Path.DirectorySeparatorChar;
        foreach (var name in settings.GetSubKeyNames())
        {
            string? executable;
            using (var entry = settings.OpenSubKey(name))
                executable = entry?.GetValue("ExecutablePath") as string;

            if (executable?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                settings.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
        }
    }

    private static void Attempt(Action step)
    {
        try
        {
            step();
        }
        catch
        {
            // best-effort: one step failing must not stop the others or the uninstall
        }
    }
}
