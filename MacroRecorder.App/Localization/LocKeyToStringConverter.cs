using System.Globalization;
using System.Windows.Data;

namespace MacroRecorder.App.Localization;

public sealed class LocKeyToStringConverter : IValueConverter
{
    public static readonly LocKeyToStringConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter as string;
        if (string.IsNullOrEmpty(key))
            return "";
        return UiLocalizerHost.Current?.GetString(key) ?? key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
