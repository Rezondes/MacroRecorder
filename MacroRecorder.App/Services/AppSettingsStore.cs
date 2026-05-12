using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacroRecorder.Wpf;

namespace MacroRecorder.App.Services;

public sealed class AppSettings
{
    [JsonPropertyName("uiCulture")]
    public string UiCulture { get; set; } = "en";

    [JsonPropertyName("appearanceTheme")]
    public string AppearanceTheme { get; set; } = "bluegrey";

    [JsonPropertyName("appearanceIsDark")]
    public bool AppearanceIsDark { get; set; }
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string SettingsFilePath => UiCultureSettings.SettingsFilePath;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                    return Normalize(loaded);
            }
        }
        catch
        {
            // fall through
        }

        return Default();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        var normalized = Normalize(settings);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(normalized, JsonOptions));
    }

    public static void SaveCultureOnly(string twoLetterIsoLanguageName)
    {
        var s = Load();
        s.UiCulture = twoLetterIsoLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        Save(s);
    }

    public static void SaveAppearanceOnly(ThemeId themeId, bool isDark)
    {
        var s = Load();
        s.AppearanceTheme = ThemeCatalog.ToStorageString(themeId);
        s.AppearanceIsDark = isDark;
        Save(s);
    }

    private static AppSettings Normalize(AppSettings s)
    {
        s.UiCulture = s.UiCulture.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        if (string.IsNullOrWhiteSpace(s.AppearanceTheme))
            s.AppearanceTheme = ThemeCatalog.ToStorageString(ThemeId.BlueGrey);
        else
            s.AppearanceTheme = ThemeCatalog.ToStorageString(ThemeCatalog.ParseThemeId(s.AppearanceTheme));
        return s;
    }

    private static AppSettings Default()
    {
        var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? "de"
            : "en";
        return new AppSettings { UiCulture = ui };
    }
}
