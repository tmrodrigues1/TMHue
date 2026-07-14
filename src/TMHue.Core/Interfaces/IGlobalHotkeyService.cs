using TMHue.Core.Models;

namespace TMHue.Core.Interfaces;

public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>Raised with the id passed to <see cref="TryRegister"/> when that hotkey fires.</summary>
    event EventHandler<string>? HotkeyPressed;

    /// <summary>Registers the given hotkey under <paramref name="id"/>, replacing any previous
    /// registration for that id. Returns false when it conflicts with another application.</summary>
    bool TryRegister(string id, HotkeyDefinition hotkey);

    void Unregister(string id);

    void UnregisterAll();
}
