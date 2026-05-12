using System.Globalization;
using System.Windows.Data;
using MacroRecorder.App.Localization;

namespace MacroRecorder.App.Converters;

public sealed class LocKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key))
            return "";
        return UiLocalizerHost.Current?.GetString(key) ?? key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
