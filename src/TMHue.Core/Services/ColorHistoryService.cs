using System.Text.Json;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;

namespace TMHue.Core.Services;

/// <summary>Keeps the most recent captured colors, collapsing consecutive duplicates by refreshing
/// their timestamp. The main window shows the first few; the "Ver mais" sidebar shows the rest.</summary>
public sealed class ColorHistoryService : IColorHistoryService
{
    public const int MaxItems = 10;
    public const int MaxPinned = 3;
    private const long MaxFileSizeBytes = 64 * 1024;

    private readonly List<CapturedColor> _items = new(MaxItems);
    private readonly string _historyFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ColorHistoryService(string historyFilePath)
    {
        _historyFilePath = historyFilePath;
    }

    public IReadOnlyList<CapturedColor> Items => _items;

    public event EventHandler? Changed;

    public void Add(CapturedColor color)
    {
        if (_items.Count > 0 && !_items[0].IsPinned &&
            _items[0].Hex.Equals(color.Hex, StringComparison.OrdinalIgnoreCase))
        {
            _items[0] = color with { CapturedAt = color.CapturedAt };
        }
        else
        {
            _items.Insert(0, color);
            TrimToCapacity();
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _items.Clear();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void TogglePin(CapturedColor color)
    {
        var index = _items.FindIndex(c =>
            c.Hex.Equals(color.Hex, StringComparison.OrdinalIgnoreCase) && c.CapturedAt == color.CapturedAt);
        if (index < 0) return;

        var item = _items[index];
        if (!item.IsPinned && _items.Count(c => c.IsPinned) >= MaxPinned)
            return;

        _items[index] = item with { IsPinned = !item.IsPinned };
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Evicts the oldest unpinned entries first, so pinned colors survive new captures
    /// instead of scrolling out with everything else.</summary>
    private void TrimToCapacity()
    {
        while (_items.Count > MaxItems)
        {
            var indexToRemove = _items.FindLastIndex(c => !c.IsPinned);
            if (indexToRemove < 0) break;
            _items.RemoveAt(indexToRemove);
        }
    }

    public void Load()
    {
        _items.Clear();
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = ReadFileWithSizeLimit();
                var loaded = JsonSerializer.Deserialize<List<CapturedColor>>(json, _jsonOptions);
                if (loaded is not null)
                    _items.AddRange(loaded.Take(MaxItems));
            }
        }
        catch (Exception) when (IsRecoverableIoOrJsonError())
        {
            BackupCorruptedFile();
            _items.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_historyFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_items, _jsonOptions);
        AtomicFileWriter.Write(_historyFilePath, json);
    }

    private void BackupCorruptedFile()
    {
        try
        {
            if (File.Exists(_historyFilePath))
                File.Copy(_historyFilePath, _historyFilePath + ".bak", overwrite: true);
        }
        catch
        {
            // Best-effort backup; corrupted state must never block startup.
        }
    }

    private string ReadFileWithSizeLimit()
    {
        var fileInfo = new FileInfo(_historyFilePath);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            File.Delete(_historyFilePath);
            throw new IOException($"History file exceeds the {MaxFileSizeBytes}-byte limit.");
        }

        return File.ReadAllText(_historyFilePath);
    }

    private static bool IsRecoverableIoOrJsonError() => true;
}
