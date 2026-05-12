using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace MacroRecorder.App.Localization;

public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return "";

        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget provideTarget)
            return UiLocalizerHost.Current?.GetString(Key) ?? Key;

        if (provideTarget.TargetObject is Setter)
            return UiLocalizerHost.Current?.GetString(Key) ?? Key;

        if (provideTarget.TargetProperty is not DependencyProperty)
            return UiLocalizerHost.Current?.GetString(Key) ?? Key;

        if (provideTarget.TargetObject is not DependencyObject)
            return UiLocalizerHost.Current?.GetString(Key) ?? Key;

        var binding = new Binding(nameof(UiCulturePulse.Tick))
        {
            Source = UiCulturePulse.Instance,
            Mode = BindingMode.OneWay,
            Converter = LocKeyToStringConverter.Instance,
            ConverterParameter = Key
        };

        return binding.ProvideValue(serviceProvider);
    }
}
