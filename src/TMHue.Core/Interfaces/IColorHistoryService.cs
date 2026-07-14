using TMHue.Core.Models;

namespace TMHue.Core.Interfaces;

public interface IColorHistoryService
{
    IReadOnlyList<CapturedColor> Items { get; }

    event EventHandler? Changed;

    void Add(CapturedColor color);

    /// <summary>Removes every entry, pinned or not.</summary>
    void Clear();

    /// <summary>Toggles the pinned state of the given entry (matched by hex + capture time), up
    /// to <see cref="TMHue.Core.Services.ColorHistoryService.MaxPinned"/> pins. A no-op if the
    /// entry isn't found, or if pinning it would exceed the cap.</summary>
    void TogglePin(CapturedColor color);

    void Load();

    void Save();
}
