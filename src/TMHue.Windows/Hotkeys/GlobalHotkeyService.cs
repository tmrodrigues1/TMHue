using System.Windows.Interop;
using TMHue.Core.Interfaces;
using TMHue.Core.Models;
using TMHue.Windows.Native;

namespace TMHue.Windows.Hotkeys;

/// <summary>
/// Registers multiple named global hotkeys via RegisterHotKey, listening for WM_HOTKEY on a
/// hidden message-only window. Backed by HwndSource so it needs no visible WPF window.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int FirstHotkeyId = 0x4A2B; // arbitrary, unique within this process

    private readonly Dictionary<string, int> _registeredIds = new();
    private int _nextNumericId = FirstHotkeyId;

    private HwndSource? _source;

    public event EventHandler<string>? HotkeyPressed;

    public bool TryRegister(string id, HotkeyDefinition hotkey)
    {
        Unregister(id);

        EnsureMessageWindow();
        if (_source is null)
            return false;

        var modifiers = ToModifierFlags(hotkey.Modifiers);
        var vk = ToVirtualKey(hotkey.Key);
        if (vk == 0)
            return false;

        var numericId = _nextNumericId++;
        var success = NativeMethods.RegisterHotKey(_source.Handle, numericId, modifiers, vk);
        if (success)
            _registeredIds[id] = numericId;

        return success;
    }

    public void Unregister(string id)
    {
        if (_source is not null && _registeredIds.TryGetValue(id, out var numericId))
        {
            NativeMethods.UnregisterHotKey(_source.Handle, numericId);
            _registeredIds.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredIds.Keys.ToList())
            Unregister(id);
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }

    private void EnsureMessageWindow()
    {
        if (_source is not null)
            return;

        var parameters = new HwndSourceParameters("TMHue.HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new nint(-3) // HWND_MESSAGE
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var numericId = wParam.ToInt32();
            foreach (var (id, registeredNumericId) in _registeredIds)
            {
                if (registeredNumericId != numericId)
                    continue;

                HotkeyPressed?.Invoke(this, id);
                handled = true;
                break;
            }
        }

        return 0;
    }

    private static uint ToModifierFlags(IEnumerable<string> modifiers)
    {
        uint flags = 0;
        foreach (var modifier in modifiers)
        {
            flags |= modifier switch
            {
                "Control" => NativeMethods.MOD_CONTROL,
                "Alt" => NativeMethods.MOD_ALT,
                "Shift" => NativeMethods.MOD_SHIFT,
                "Windows" => NativeMethods.MOD_WIN,
                _ => 0u
            };
        }

        return flags;
    }

    private static uint ToVirtualKey(string key)
    {
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z') return c;
            if (c is >= '0' and <= '9') return c;
        }

        return key.ToUpperInvariant() switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20,
            "TAB" => 0x09, "ENTER" => 0x0D, "ESCAPE" => 0x1B, "BACKSPACE" => 0x08,
            "INSERT" => 0x2D, "DELETE" => 0x2E, "HOME" => 0x24, "END" => 0x23,
            "PAGEUP" => 0x21, "PAGEDOWN" => 0x22,
            "LEFT" => 0x25, "UP" => 0x26, "RIGHT" => 0x27, "DOWN" => 0x28,
            _ => 0
        };
    }
}
