namespace TMHue.Core.Models;

/// <summary>Serializable description of a global hotkey. Modifiers use RegisterHotKey names.</summary>
public sealed class HotkeyDefinition
{
    public List<string> Modifiers { get; set; } = new() { "Control", "Alt" };

    public string Key { get; set; } = "T";

    public static HotkeyDefinition Default => new()
    {
        Modifiers = new List<string> { "Control", "Alt" },
        Key = "T"
    };

    public static HotkeyDefinition DefaultOpenApp => new()
    {
        Modifiers = new List<string> { "Control", "Alt" },
        Key = "O"
    };

    public static HotkeyDefinition DefaultOpenContrastChecker => new()
    {
        Modifiers = new List<string> { "Control", "Alt" },
        Key = "K"
    };

    public override string ToString() =>
        string.Join(" + ", Modifiers.Select(m => m == "Control" ? "CTRL" : m).Append(Key));
}
