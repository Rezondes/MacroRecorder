using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MacroRecorder.App.Services;

/// <summary>
/// UI culture helpers; persisted values live in <see cref="AppSettingsStore"/> (settings.json).
/// </summary>
public static class UiCultureSettings
{
    private const string SettingsFileName = "settings.json";

    public static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacroRecorderByRezondes",
        SettingsFileName);

    public static CultureInfo ResolveUiCulture()
    {
        var raw = AppSettingsStore.Load().UiCulture;
        return raw.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("de")
            : CultureInfo.GetCultureInfo("en");
    }

    public static void SaveUiCulturePreference(string twoLetterIsoLanguageName) =>
        AppSettingsStore.SaveCultureOnly(twoLetterIsoLanguageName);
}
