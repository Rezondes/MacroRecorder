using System.Globalization;
using System.Resources;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Services;

/// <summary>
/// Loads strings from embedded <c>Localization/UiStrings*.resx</c> (neutral English + German satellite).
/// </summary>
public sealed class ResxUiLocalizer : IUiLocalizer
{
    private static readonly ResourceManager ResourceManager = new(
        "MacroRecorder.App.Localization.UiStrings",
        typeof(ResxUiLocalizer).Assembly);

    public ResxUiLocalizer()
    {
        CurrentUiCulture = UiCultureSettings.ResolveUiCulture();
        CultureInfo.DefaultThreadCurrentUICulture = CurrentUiCulture;
        CultureInfo.CurrentUICulture = CurrentUiCulture;
    }

    public CultureInfo CurrentUiCulture { get; }

    public string GetString(string key)
    {
        var value = ResourceManager.GetString(key, CurrentUiCulture);
        return string.IsNullOrEmpty(value) ? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en")) ?? key : value;
    }

    public string GetString(string key, params object?[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(CurrentUiCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
