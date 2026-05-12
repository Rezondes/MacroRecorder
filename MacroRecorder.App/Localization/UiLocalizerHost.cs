using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Localization;

/// <summary>
/// Holds the active <see cref="IUiLocalizer"/> for WPF markup extensions (resolved before windows are shown).
/// </summary>
public static class UiLocalizerHost
{
    public static IUiLocalizer? Current { get; set; }
}
