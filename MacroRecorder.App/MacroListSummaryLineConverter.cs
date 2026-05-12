using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MacroRecorder.App.Localization;

namespace MacroRecorder.App;

public sealed class MacroListSummaryLineConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return "";
        if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            return "";
        if (values[0] is not int count || values[1] is not DateTimeOffset modified)
            return "";
        var loc = UiLocalizerHost.Current;
        return loc is null ? "" : loc.GetString("Main_MacroListEventsFormat", count, modified);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
