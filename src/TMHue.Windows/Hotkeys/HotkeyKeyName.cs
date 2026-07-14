using System.Windows.Input;

namespace TMHue.Windows.Hotkeys;

/// <summary>Maps a WPF <see cref="Key"/> to the string format used by <see cref="HotkeyDefinition"/>
/// and understood by <see cref="GlobalHotkeyService"/>'s virtual-key lookup.</summary>
public static class HotkeyKeyName
{
    public static string? From(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return key.ToString();

        if (key >= Key.D0 && key <= Key.D9)
            return key.ToString()[1..];

        if (key >= Key.F1 && key <= Key.F12)
            return key.ToString();

        return key switch
        {
            Key.Space => "SPACE",
            Key.Tab => "TAB",
            Key.Enter => "ENTER",
            Key.Escape => "ESCAPE",
            Key.Back => "BACKSPACE",
            Key.Insert => "INSERT",
            Key.Delete => "DELETE",
            Key.Home => "HOME",
            Key.End => "END",
            Key.PageUp => "PAGEUP",
            Key.PageDown => "PAGEDOWN",
            Key.Left => "LEFT",
            Key.Up => "UP",
            Key.Right => "RIGHT",
            Key.Down => "DOWN",
            _ => null
        };
    }

    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System;
}
