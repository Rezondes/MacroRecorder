namespace MacroRecorder.Wpf;

public sealed record ThemeCatalogEntry(ThemeId Id, string DisplayNameKey, IReadOnlyList<string> SwatchHexes);

/// <summary>Static metadata for settings UI (swatch preview, localization keys for display name).</summary>
public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeCatalogEntry> Entries { get; } =
    [
        new(ThemeId.BlueGrey, "Theme_Name_BlueGrey", ["#607D8B", "#546E7A", "#78909C", "#B0BEC5", "#FFFFFF"]),
        new(ThemeId.Indigo, "Theme_Name_Indigo", ["#3F51B5", "#303F9F", "#5C6BC0", "#9FA8DA", "#FFFFFF"]),
        new(ThemeId.Teal, "Theme_Name_Teal", ["#009688", "#00796B", "#26A69A", "#80CBC4", "#FFFFFF"]),
        new(ThemeId.DeepPurple, "Theme_Name_DeepPurple", ["#673AB7", "#512DA8", "#7E57C2", "#B39DDB", "#FFFFFF"]),
        new(ThemeId.Amber, "Theme_Name_Amber", ["#FFC107", "#FFA000", "#FFD54F", "#FFECB3", "#212121"]),
    ];

    public static ThemeId ParseThemeId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ThemeId.BlueGrey;
        return raw.Trim().ToLowerInvariant() switch
        {
            "indigo" => ThemeId.Indigo,
            "teal" => ThemeId.Teal,
            "deeppurple" => ThemeId.DeepPurple,
            "amber" => ThemeId.Amber,
            _ => ThemeId.BlueGrey
        };
    }

    public static string ToStorageString(ThemeId id) =>
        id switch
        {
            ThemeId.Indigo => "indigo",
            ThemeId.Teal => "teal",
            ThemeId.DeepPurple => "deeppurple",
            ThemeId.Amber => "amber",
            _ => "bluegrey"
        };
}
