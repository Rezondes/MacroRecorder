using System.Globalization;
using System.Linq;
using System.Windows.Data;
using MacroRecorder.App.Localization;

namespace MacroRecorder.App.Converters;

/// <summary>
/// Resolves a RESX key via <see cref="UiLocalizerHost"/> and re-runs when <see cref="UiCulturePulse.Tick"/> changes.
/// Use <see cref="ConverterParameter"/> as the key with a single binding to <c>Tick</c>, or omit the parameter and pass
/// the key as the first value and <c>Tick</c> as the second.
/// </summary>
public sealed class LocResxWithPulseMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        string? key = parameter as string;
        if (!string.IsNullOrEmpty(key))
            return UiLocalizerHost.Current?.GetString(key) ?? key;

        key = values.FirstOrDefault(v => v is string) as string;
        if (string.IsNullOrEmpty(key))
            return "";
        return UiLocalizerHost.Current?.GetString(key) ?? key;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
