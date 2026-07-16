using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Brushes = System.Windows.Media.Brushes;

namespace TMHue.App.Converters;

/// <summary>Collapses a bound element when the source string is null/empty/whitespace; used for
/// optional error and suggestion messages that should only take up space when present.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Hides a bound element when the source string is null/empty/whitespace, using
/// <see cref="Visibility.Hidden"/> rather than Collapsed so the reserved layout space stays
/// constant — used for the recommendation block, which should vanish visually once the contrast
/// already passes without shrinking the window.</summary>
public sealed class StringToHiddenVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Hidden : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Collapses a bound element when the source boolean is true — the complement of the
/// stock BooleanToVisibilityConverter, for mutually exclusive UI states (e.g. the "Verificar"
/// button hiding while the update consent buttons are shown).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Renders a WCAG pass/fail boolean as short Portuguese labels for the contrast checker.</summary>
public sealed class PassFailToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Aprova" : "Reprova";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Renders a WCAG pass/fail boolean as a green (pass) or red (fail) brush.</summary>
public sealed class PassFailToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Brushes.MediumSeaGreen : Brushes.IndianRed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
