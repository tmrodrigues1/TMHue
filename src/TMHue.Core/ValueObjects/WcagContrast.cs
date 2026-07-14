namespace TMHue.Core.ValueObjects;

/// <summary>WCAG 2.x contrast thresholds (see https://www.w3.org/TR/WCAG21/#contrast-minimum).</summary>
public static class WcagLevel
{
    public const double AaNormalText = 4.5;
    public const double AaaNormalText = 7.0;
    public const double AaLargeText = 3.0;
    public const double AaaLargeText = 4.5;
}

/// <summary>Contrast ratio between two colors plus the pass/fail result for every standard WCAG
/// text-size/level combination.</summary>
public readonly record struct ContrastEvaluation(
    double Ratio,
    bool PassesAaNormalText,
    bool PassesAaaNormalText,
    bool PassesAaLargeText,
    bool PassesAaaLargeText);

/// <summary>The smallest one-sided change to the foreground (text) color's luminance that would
/// bring the pair up to a target contrast ratio, keeping the background fixed.</summary>
public readonly record struct ContrastSuggestion(bool Darken, double PercentChange);

/// <summary>Relative luminance and contrast ratio calculations per the WCAG 2.x formula. Kept
/// independent of any UI framework so it can be unit tested in isolation.</summary>
public static class WcagContrast
{
    /// <summary>Relative luminance of an sRGB color, in the 0..1 range.</summary>
    public static double RelativeLuminance(RgbColor color)
    {
        var r = Linearize(color.Red / 255.0);
        var g = Linearize(color.Green / 255.0);
        var b = Linearize(color.Blue / 255.0);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double Linearize(double channel) =>
        channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    /// <summary>WCAG contrast ratio between two colors, always &gt;= 1. Order of the arguments
    /// does not matter.</summary>
    public static double ContrastRatio(RgbColor a, RgbColor b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Computes the contrast ratio between <paramref name="foreground"/> (typically
    /// text) and <paramref name="background"/>, along with pass/fail against every standard
    /// WCAG threshold.</summary>
    public static ContrastEvaluation Evaluate(RgbColor foreground, RgbColor background)
    {
        var ratio = ContrastRatio(foreground, background);
        return new ContrastEvaluation(
            Ratio: ratio,
            PassesAaNormalText: ratio >= WcagLevel.AaNormalText,
            PassesAaaNormalText: ratio >= WcagLevel.AaaNormalText,
            PassesAaLargeText: ratio >= WcagLevel.AaLargeText,
            PassesAaaLargeText: ratio >= WcagLevel.AaaLargeText);
    }

    /// <summary>Suggests the smallest luminance adjustment to the foreground color alone
    /// (darkening or lightening it) that would raise the pair's contrast up to
    /// <paramref name="targetRatio"/>, with the background held fixed. Returns null when the
    /// pair already meets the target, or when the target is unattainable by adjusting the
    /// foreground's luminance alone (0..1 range exhausted in both directions).</summary>
    public static ContrastSuggestion? SuggestForegroundAdjustment(RgbColor foreground, RgbColor background, double targetRatio)
    {
        var currentForeground = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);

        if (ContrastRatio(foreground, background) >= targetRatio) return null;

        double? darkenCandidate = null;
        var darkenTarget = (backgroundLuminance + 0.05) / targetRatio - 0.05;
        if (darkenTarget >= 0 && darkenTarget < currentForeground)
            darkenCandidate = darkenTarget;

        double? lightenCandidate = null;
        var lightenTarget = targetRatio * (backgroundLuminance + 0.05) - 0.05;
        if (lightenTarget <= 1 && lightenTarget > currentForeground)
            lightenCandidate = lightenTarget;

        if (darkenCandidate is null && lightenCandidate is null) return null;

        bool darken;
        double newLuminance;
        if (darkenCandidate is { } dCandidate && lightenCandidate is { } lCandidate)
        {
            var darkenDelta = Math.Abs(dCandidate - currentForeground);
            var lightenDelta = Math.Abs(lCandidate - currentForeground);
            darken = darkenDelta <= lightenDelta;
            newLuminance = darken ? dCandidate : lCandidate;
        }
        else if (darkenCandidate is { } onlyDarken)
        {
            darken = true;
            newLuminance = onlyDarken;
        }
        else
        {
            darken = false;
            newLuminance = lightenCandidate!.Value;
        }

        var percent = currentForeground <= 0.0001
            ? 100.0
            : Math.Abs(newLuminance - currentForeground) / currentForeground * 100.0;

        return new ContrastSuggestion(darken, Math.Clamp(percent, 0, 100));
    }
}
