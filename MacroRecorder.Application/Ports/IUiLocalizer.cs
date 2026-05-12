using System.Globalization;

namespace MacroRecorder.Application.Ports;

/// <summary>
/// Resolves user-visible UI strings for the current UI culture (supported: de, en).
/// </summary>
public interface IUiLocalizer
{
    CultureInfo CurrentUiCulture { get; }

    /// <summary>Raised after <see cref="ApplyUiCulture"/> completes.</summary>
    event EventHandler? UiCultureChanged;

    /// <summary>
    /// Persists the preference, updates thread UI culture, and notifies listeners so the UI can refresh without restart.
    /// </summary>
    void ApplyUiCulture(CultureInfo culture);

    /// <summary>Returns the localized string for <paramref name="key"/> or the key itself if missing.</summary>
    string GetString(string key);

    /// <summary>Formats a localized template (composite format, invariant placeholders).</summary>
    string GetString(string key, params object?[] args);
}
