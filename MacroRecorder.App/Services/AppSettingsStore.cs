using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacroRecorder.Wpf;

namespace MacroRecorder.App.Services;

/// <summary>Persisted <see cref="System.Windows.Window"/> bounds for the main shell (DIP).</summary>
public sealed class MainWindowPlacement
{
    [JsonPropertyName("left")]
    public double Left { get; set; }

    [JsonPropertyName("top")]
    public double Top { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }
}

public sealed class AppSettings
{
    [JsonPropertyName("uiCulture")]
    public string UiCulture { get; set; } = "en";

    [JsonPropertyName("appearanceTheme")]
    public string AppearanceTheme { get; set; } = "bluegrey";

    [JsonPropertyName("appearanceIsDark")]
    public bool AppearanceIsDark { get; set; }

    /// <summary>Minimum Euclidean pixel delta between consecutive stored mouse moves (recording).</summary>
    [JsonPropertyName("recordingMouseMoveMinPixels")]
    public int RecordingMouseMoveMinPixels { get; set; } = 10;

    /// <summary>Wait this many milliseconds after play begins before emitting events; same window ignores user interrupt (0 = off).</summary>
    [JsonPropertyName("playbackUserInterruptGraceMs")]
    public int PlaybackUserInterruptGraceMs { get; set; } = 1000;

    /// <summary>Last normal (or restore) bounds of the main window; null = use default placement.</summary>
    [JsonPropertyName("mainWindowPlacement")]
    public MainWindowPlacement? MainWindowPlacement { get; set; }
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

    public static void SaveMainWindowPlacementOnly(MainWindowPlacement placement)
    {
        var s = Load();
        s.MainWindowPlacement = placement;
        Save(s);
    }

    private static AppSettings Normalize(AppSettings s)
    {
        s.UiCulture = s.UiCulture.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        if (string.IsNullOrWhiteSpace(s.AppearanceTheme))
            s.AppearanceTheme = ThemeCatalog.ToStorageString(ThemeId.BlueGrey);
        else
            s.AppearanceTheme = ThemeCatalog.ToStorageString(ThemeCatalog.ParseThemeId(s.AppearanceTheme));
        if (s.RecordingMouseMoveMinPixels < 1)
            s.RecordingMouseMoveMinPixels = 1;
        if (s.RecordingMouseMoveMinPixels > 10_000)
            s.RecordingMouseMoveMinPixels = 10_000;
        if (s.PlaybackUserInterruptGraceMs < 0)
            s.PlaybackUserInterruptGraceMs = 0;
        if (s.PlaybackUserInterruptGraceMs > 300_000)
            s.PlaybackUserInterruptGraceMs = 300_000;
        if (s.MainWindowPlacement is { } placement)
        {
            if (!IsFinitePlacement(placement))
                s.MainWindowPlacement = null;
            else
            {
                placement.Width = Math.Clamp(placement.Width, 640, 16_000);
                placement.Height = Math.Clamp(placement.Height, 400, 16_000);
            }
        }

        return s;
    }

    private static bool IsFinitePlacement(MainWindowPlacement placement) =>
        !double.IsNaN(placement.Left) &&
        !double.IsNaN(placement.Top) &&
        !double.IsNaN(placement.Width) &&
        !double.IsNaN(placement.Height) &&
        !double.IsInfinity(placement.Left) &&
        !double.IsInfinity(placement.Top) &&
        !double.IsInfinity(placement.Width) &&
        !double.IsInfinity(placement.Height);

    private static AppSettings Default()
    {
        var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? "de"
            : "en";
        return new AppSettings { UiCulture = ui, RecordingMouseMoveMinPixels = 10, PlaybackUserInterruptGraceMs = 1000 };
    }
}
