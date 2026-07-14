using TMHue.Core.Models;
using Xunit;

namespace TMHue.UnitTests;

public class HotkeyDefinitionTests
{
    [Fact]
    public void Default_IsControlAltT()
    {
        var hotkey = HotkeyDefinition.Default;
        Assert.Equal(new[] { "Control", "Alt" }, hotkey.Modifiers);
        Assert.Equal("T", hotkey.Key);
    }

    [Fact]
    public void ToString_JoinsModifiersAndKey()
    {
        var hotkey = new HotkeyDefinition { Modifiers = new List<string> { "Control", "Shift" }, Key = "X" };
        Assert.Equal("CTRL + Shift + X", hotkey.ToString());
    }
}
