using System.Globalization;
using System.Windows.Data;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace TMHue.App.Converters;

/// <summary>Highlights a pinned swatch with the accent border color; transparent otherwise.</summary>
public sealed class PinBorderBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush PinnedBrush = CreateFrozen();

    private static SolidColorBrush CreateFrozen()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x9D, 0x8C, 0xFF));
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? PinnedBrush : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
