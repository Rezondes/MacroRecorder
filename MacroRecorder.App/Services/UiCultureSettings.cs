using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MacroRecorder.App.Services;

/// <summary>
/// Reads optional UI culture from %LocalAppData%/MacroRecorderByRezondes/settings.json ({ "uiCulture": "de" | "en" }).
/// </summary>
public static class UiCultureSettings
{
    private const string SettingsFileName = "settings.json";

    public static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacroRecorderByRezondes",
        SettingsFileName);

    /// <summary>Supported UI cultures for the app.</summary>
    public static CultureInfo ResolveUiCulture()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFilePath));
                if (doc.RootElement.TryGetProperty("uiCulture", out var cultureElement))
                {
                    var raw = cultureElement.GetString()?.Trim().ToLowerInvariant();
                    if (raw is "de" or "de-de")
                        return CultureInfo.GetCultureInfo("de");
                    if (raw is "en" or "en-us")
                        return CultureInfo.GetCultureInfo("en");
                }
            }
        }
        catch
        {
            // fall through to system default
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("de")
            : CultureInfo.GetCultureInfo("en");
    }

    public static void SaveUiCulturePreference(string twoLetterIsoLanguageName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        var normalized = twoLetterIsoLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        var json = JsonSerializer.Serialize(new Dictionary<string, string> { ["uiCulture"] = normalized });
        File.WriteAllText(SettingsFilePath, json);
    }
}
