using System.Globalization;
using System.Windows.Data;
using TMHue.Core.Models;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;

namespace TMHue.App.Converters;

public sealed class CapturedColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CapturedColor c) return Brushes.Transparent;

        var brush = new SolidColorBrush(Color.FromRgb(c.Red, c.Green, c.Blue));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
