using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MacroRecorder.App.Converters;

public sealed class HexStringToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s))
            return Brushes.Transparent;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(s)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
