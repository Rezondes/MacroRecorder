using System.Globalization;
using System.Resources;
using MacroRecorder.App.Localization;
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

    private CultureInfo _currentUiCulture;

    public ResxUiLocalizer()
    {
        _currentUiCulture = UiCultureSettings.ResolveUiCulture();
        CultureInfo.DefaultThreadCurrentUICulture = _currentUiCulture;
        CultureInfo.CurrentUICulture = _currentUiCulture;
    }

    public CultureInfo CurrentUiCulture => _currentUiCulture;

    public event EventHandler? UiCultureChanged;

    public void ApplyUiCulture(CultureInfo culture)
    {
        var normalized = NormalizeUiCulture(culture);
        AppSettingsStore.SaveCultureOnly(normalized.TwoLetterISOLanguageName);
        CultureInfo.DefaultThreadCurrentUICulture = normalized;
        CultureInfo.CurrentUICulture = normalized;
        _currentUiCulture = normalized;
        UiCulturePulse.Instance.Bump();
        UiCultureChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CultureInfo NormalizeUiCulture(CultureInfo culture)
    {
        var name = culture.TwoLetterISOLanguageName;
        return name.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("de")
            : CultureInfo.GetCultureInfo("en");
    }

    public string GetString(string key)
    {
        var value = ResourceManager.GetString(key, _currentUiCulture);
        return string.IsNullOrEmpty(value) ? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en")) ?? key : value;
    }

    public string GetString(string key, params object?[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(_currentUiCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
