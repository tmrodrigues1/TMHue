using System.Globalization;
using System.Windows.Data;

namespace TMHue.App.Converters;

/// <summary>Shrinks the primary color readout's font as its text gets longer, so HEX (short),
/// RGB and HSL (longer) values all fit the fixed-width main window without ellipsis-truncating —
/// the copy button next to it must stay reachable regardless of which format is active.</summary>
public sealed class TextLengthToFontSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var length = (value as string)?.Length ?? 0;
        return length switch
        {
            <= 7 => 21.0,
            <= 13 => 17.0,
            <= 19 => 14.0,
            _ => 12.0
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
