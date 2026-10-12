using System.Text.Json;
using TMHue.Core.Models;
using Xunit;

namespace TMHue.UnitTests;

public class AppSettingsTests
{
    // Mirrors what a hand-edited or damaged settings.json produces: values that are valid JSON
    // but not valid settings.
    private static AppSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(settings);
        return settings;
    }

    [Fact]
    public void Normalize_KeepsValidSettingsUntouched()
    {
        var settings = new AppSettings
        {
            Theme = AppTheme.Light,
            Language = "en-US",
            CopyFormat = CopyFormat.Hsl,
            SampleAreaSize = 11,
            ColorChangeTolerance = 8,
            Hotkey = new HotkeyDefinition { Modifiers = new List<string> { "Control", "Shift" }, Key = "F8" },
            WindowLeft = -1200,
            WindowTop = 40
        };

        settings.Normalize();

        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.Equal("en-US", settings.Language);
        Assert.Equal(CopyFormat.Hsl, settings.CopyFormat);
        Assert.Equal(11, settings.SampleAreaSize);
        Assert.Equal(8, settings.ColorChangeTolerance);
        Assert.Equal(new[] { "Control", "Shift" }, settings.Hotkey.Modifiers);
        Assert.Equal("F8", settings.Hotkey.Key);
        Assert.Equal(-1200, settings.WindowLeft);
        Assert.Equal(40, settings.WindowTop);
    }

    [Fact]
    public void Normalize_ReplacesNullHotkeysWithTheirOwnDefaults()
    {
        var settings = Parse("""
            { "Hotkey": null, "OpenAppHotkey": null, "ContrastCheckerHotkey": null,
              "HarmonyHotkey": null, "PaletteExtractorHotkey": null }
            """);

        settings.Normalize();

        Assert.Equal("T", settings.Hotkey.Key);
        Assert.Equal("O", settings.OpenAppHotkey.Key);
        Assert.Equal("K", settings.ContrastCheckerHotkey.Key);
        Assert.Equal("H", settings.HarmonyHotkey.Key);
        Assert.Equal("P", settings.PaletteExtractorHotkey.Key);
    }

    [Fact]
    public void Normalize_RepairsHotkeyWithNullModifiersOrEmptyKey()
    {
        var settings = Parse("""
            { "Hotkey": { "Modifiers": null, "Key": "F8" },
              "OpenAppHotkey": { "Modifiers": ["Shift"], "Key": " " },
              "HarmonyHotkey": { "Modifiers": ["Control"], "Key": null } }
            """);

        settings.Normalize();

        Assert.Equal(new[] { "Control", "Alt" }, settings.Hotkey.Modifiers);
        Assert.Equal("F8", settings.Hotkey.Key);
        Assert.Equal(new[] { "Shift" }, settings.OpenAppHotkey.Modifiers);
        Assert.Equal("O", settings.OpenAppHotkey.Key);
        Assert.Equal("H", settings.HarmonyHotkey.Key);
    }

    [Fact]
    public void Normalize_NeverLeavesAHotkeyWithoutARecognizedModifier()
    {
        var settings = Parse("""
            { "Hotkey": { "Modifiers": [], "Key": "T" },
              "OpenAppHotkey": { "Modifiers": [null, "Hyper"], "Key": "O" } }
            """);

        settings.Normalize();

        Assert.Equal(new[] { "Control", "Alt" }, settings.Hotkey.Modifiers);
        Assert.Equal(new[] { "Control", "Alt" }, settings.OpenAppHotkey.Modifiers);
    }

    [Fact]
    public void Normalize_ResetsOutOfRangeEnumsAndMissingLanguage()
    {
        var settings = Parse("""{ "Theme": 99, "CopyFormat": -1, "ColorOutput": 99, "Language": null }""");

        settings.Normalize();

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(CopyFormat.Hex, settings.CopyFormat);
        Assert.Equal(CaptureColorOutput.Rendered, settings.ColorOutput);
        Assert.Equal("pt-BR", settings.Language);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Normalize_DropsNonFiniteWindowPosition(double value)
    {
        var settings = new AppSettings { WindowLeft = value, WindowTop = value };

        settings.Normalize();

        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
    }
}
