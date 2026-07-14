using System.IO;
using System.Text.Json;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Core.Services;

namespace TMHue.Windows.Persistence;

/// <summary>Persists AppSettings as JSON in %LocalAppData%\TMHue. Corrupted files are backed up and replaced with defaults.</summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private const long MaxFileSizeBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public SettingsRepository(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return AppSettings.CreateDefault();

        try
        {
            var json = ReadFileWithSizeLimit();
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
        }
        catch (Exception)
        {
            BackupCorruptedFile();
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        AtomicFileWriter.Write(_filePath, json);
    }

    private void BackupCorruptedFile()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Copy(_filePath, _filePath + ".bak", overwrite: true);
        }
        catch
        {
            // Best-effort; must never block startup.
        }
    }

    private string ReadFileWithSizeLimit()
    {
        var fileInfo = new FileInfo(_filePath);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            File.Delete(_filePath);
            throw new IOException($"Settings file exceeds the {MaxFileSizeBytes}-byte limit.");
        }

        return File.ReadAllText(_filePath);
    }
}
