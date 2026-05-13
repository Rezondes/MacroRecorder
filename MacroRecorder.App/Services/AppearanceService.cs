using System.Windows;
using MacroRecorder.Wpf;
using MacroRecorder.Wpf.Controls;

namespace MacroRecorder.App.Services;

public sealed class AppearanceService
{
    private ResourceDictionary? _themeDictionary;

    public AppearanceService()
    {
        var s = AppSettingsStore.Load();
        LastPersistedTheme = ThemeCatalog.ParseThemeId(s.AppearanceTheme);
        LastPersistedIsDark = s.AppearanceIsDark;
        PreviewTheme = LastPersistedTheme;
        PreviewIsDark = LastPersistedIsDark;
    }

    public ThemeId PreviewTheme { get; private set; }

    public bool PreviewIsDark { get; private set; }

    public ThemeId LastPersistedTheme { get; private set; }

    public bool LastPersistedIsDark { get; private set; }

    public bool HasPendingChanges =>
        PreviewTheme != LastPersistedTheme || PreviewIsDark != LastPersistedIsDark;

    public event EventHandler? PreviewChanged;

    public void Initialize(global::System.Windows.Application app)
    {
        UiToolTip.ConfigureToolTipService();
        var controls = ThemeResourceBuilder.LoadAppControls();
        app.Resources.MergedDictionaries.Add(controls);
        _themeDictionary = ThemeResourceBuilder.Build(PreviewTheme, PreviewIsDark);
        app.Resources.MergedDictionaries.Add(_themeDictionary);
    }

    public void ApplyPreview(ThemeId theme, bool isDark)
    {
        PreviewTheme = theme;
        PreviewIsDark = isDark;
        ReplaceThemeDictionary(global::System.Windows.Application.Current);
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Persist()
    {
        AppSettingsStore.SaveAppearanceOnly(PreviewTheme, PreviewIsDark);
        LastPersistedTheme = PreviewTheme;
        LastPersistedIsDark = PreviewIsDark;
    }

    public void RevertToSaved()
    {
        PreviewTheme = LastPersistedTheme;
        PreviewIsDark = LastPersistedIsDark;
        ReplaceThemeDictionary(global::System.Windows.Application.Current);
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadPersistedFromDisk()
    {
        var s = AppSettingsStore.Load();
        LastPersistedTheme = ThemeCatalog.ParseThemeId(s.AppearanceTheme);
        LastPersistedIsDark = s.AppearanceIsDark;
        PreviewTheme = LastPersistedTheme;
        PreviewIsDark = LastPersistedIsDark;
        ReplaceThemeDictionary(global::System.Windows.Application.Current);
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceThemeDictionary(global::System.Windows.Application? app)
    {
        if (app is null)
            return;
        if (_themeDictionary is not null)
            app.Resources.MergedDictionaries.Remove(_themeDictionary);
        _themeDictionary = ThemeResourceBuilder.Build(PreviewTheme, PreviewIsDark);
        app.Resources.MergedDictionaries.Add(_themeDictionary);
    }
}
