using TMHue.Core.ValueObjects;
using Xunit;

namespace TMHue.UnitTests;

public class RgbColorTests
{
    [Fact]
    public void ToHex_FormatsUppercaseByDefault()
    {
        var color = new RgbColor(47, 128, 237);
        Assert.Equal("#2F80ED", color.ToHex());
    }

    [Fact]
    public void ToHex_CanFormatLowercase()
    {
        var color = new RgbColor(47, 128, 237);
        Assert.Equal("#2f80ed", color.ToHex(uppercase: false));
    }

    [Theory]
    [InlineData("#2F80ED", true)]
    [InlineData("2F80ED", true)]
    [InlineData("#GGGGGG", false)]
    [InlineData("#2F80E", false)]
    [InlineData("", false)]
    public void TryParseHex_ValidatesInput(string input, bool expected)
    {
        Assert.Equal(expected, RgbColor.TryParseHex(input, out _));
    }
}
