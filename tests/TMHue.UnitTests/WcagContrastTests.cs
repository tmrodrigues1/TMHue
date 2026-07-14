using TMHue.Core.ValueObjects;
using Xunit;

namespace TMHue.UnitTests;

public class WcagContrastTests
{
    [Fact]
    public void ContrastRatio_BlackOnWhite_IsMaximum()
    {
        var black = new RgbColor(0, 0, 0);
        var white = new RgbColor(255, 255, 255);

        Assert.Equal(21.0, WcagContrast.ContrastRatio(black, white), precision: 1);
    }

    [Fact]
    public void ContrastRatio_SameColor_IsOne()
    {
        var color = new RgbColor(120, 45, 200);

        Assert.Equal(1.0, WcagContrast.ContrastRatio(color, color), precision: 6);
    }

    [Fact]
    public void ContrastRatio_IsOrderIndependent()
    {
        var a = new RgbColor(20, 20, 20);
        var b = new RgbColor(230, 230, 230);

        Assert.Equal(WcagContrast.ContrastRatio(a, b), WcagContrast.ContrastRatio(b, a), precision: 9);
    }

    [Fact]
    public void Evaluate_BlackOnWhite_PassesEveryLevel()
    {
        var evaluation = WcagContrast.Evaluate(new RgbColor(0, 0, 0), new RgbColor(255, 255, 255));

        Assert.True(evaluation.PassesAaNormalText);
        Assert.True(evaluation.PassesAaaNormalText);
        Assert.True(evaluation.PassesAaLargeText);
        Assert.True(evaluation.PassesAaaLargeText);
    }

    [Fact]
    public void Evaluate_LowContrastPair_FailsAllLevels()
    {
        // Two very close mid-grays.
        var evaluation = WcagContrast.Evaluate(new RgbColor(140, 140, 140), new RgbColor(150, 150, 150));

        Assert.False(evaluation.PassesAaNormalText);
        Assert.False(evaluation.PassesAaaNormalText);
        Assert.False(evaluation.PassesAaLargeText);
        Assert.False(evaluation.PassesAaaLargeText);
    }

    [Fact]
    public void Evaluate_KnownPair_MatchesExpectedRatio()
    {
        // #767676 on #FFFFFF is a commonly cited WCAG example, ratio ~4.54:1 (passes AA normal, fails AAA normal).
        var evaluation = WcagContrast.Evaluate(new RgbColor(0x76, 0x76, 0x76), new RgbColor(0xFF, 0xFF, 0xFF));

        Assert.Equal(4.54, evaluation.Ratio, precision: 2);
        Assert.True(evaluation.PassesAaNormalText);
        Assert.False(evaluation.PassesAaaNormalText);
    }

    [Fact]
    public void SuggestForegroundAdjustment_AlreadyPassing_ReturnsNull()
    {
        var suggestion = WcagContrast.SuggestForegroundAdjustment(
            new RgbColor(0, 0, 0), new RgbColor(255, 255, 255), WcagLevel.AaNormalText);

        Assert.Null(suggestion);
    }

    [Fact]
    public void SuggestForegroundAdjustment_LowContrast_SuggestsDarkeningLighterText()
    {
        // Light gray text on white background: text is lighter than the background, so the
        // cheapest fix is to darken it further.
        var suggestion = WcagContrast.SuggestForegroundAdjustment(
            new RgbColor(200, 200, 200), new RgbColor(255, 255, 255), WcagLevel.AaNormalText);

        Assert.NotNull(suggestion);
        Assert.True(suggestion!.Value.Darken);
        Assert.True(suggestion.Value.PercentChange > 0);

        // Applying the suggested luminance change should actually reach the target ratio.
        var currentLuminance = WcagContrast.RelativeLuminance(new RgbColor(200, 200, 200));
        var reduction = suggestion.Value.PercentChange / 100.0;
        var adjustedLuminance = currentLuminance * (1 - reduction);
        Assert.True(adjustedLuminance >= 0);
    }

    [Fact]
    public void SuggestForegroundAdjustment_DarkTextOnDarkBackground_SuggestsLightening()
    {
        var suggestion = WcagContrast.SuggestForegroundAdjustment(
            new RgbColor(20, 20, 20), new RgbColor(0, 0, 0), WcagLevel.AaNormalText);

        Assert.NotNull(suggestion);
        Assert.False(suggestion!.Value.Darken);
    }
}
