using System.Windows.Markup;
using MacroRecorder.Application.Ports;

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
        return UiLocalizerHost.Current?.GetString(Key) ?? Key;
    }
}
