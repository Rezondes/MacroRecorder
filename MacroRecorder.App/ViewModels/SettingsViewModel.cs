using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using MacroRecorder.Wpf;

namespace MacroRecorder.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public readonly record struct LanguageOption(string Code, string DisplayName);

    private readonly IUiLocalizer _loc;
    private readonly AppearanceService _appearance;
    private readonly IUserDialogService _dialogs;

    private int _selectedSettingsTabIndex;

    /// <summary>0 = General, 1 = Visuals. OneWay-bound from VM; tab changes use <see cref="TryChangeSettingsTab"/>.</summary>
    public int SelectedSettingsTabIndex => _selectedSettingsTabIndex;

    /// <summary>
    /// Applies a tab switch after optional unsaved prompt. On cancel returns false and the view must reset
    /// the tab control selection to match <see cref="SelectedSettingsTabIndex"/>.
    /// </summary>
    public bool TryChangeSettingsTab(int newIndex)
    {
        if (newIndex < 0 || newIndex > 1)
            return false;
        if (newIndex == _selectedSettingsTabIndex)
            return true;

        if (HasUnsavedSettingsChanges && !TryConfirmLeavePendingSettings())
            return false;

        SetProperty(ref _selectedSettingsTabIndex, newIndex);
        return true;
    }

    public SettingsViewModel(IUiLocalizer loc, AppearanceService appearance, IUserDialogService dialogs)
    {
        _loc = loc;
        _appearance = appearance;
        _dialogs = dialogs;
        _appearance.PreviewChanged += OnAppearancePreviewChanged;
        _loc.UiCultureChanged += (_, _) => RebuildLanguageOptions();
        LoadStateFromPreferences();
    }

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new();

    public IReadOnlyList<ThemeCatalogEntry> ThemeEntries => ThemeCatalog.Entries;

    public bool HasUnsavedSettingsChanges =>
        HasPendingLanguageChange() || _appearance.HasPendingChanges;

    public bool IsLightMode
    {
        get => !_appearance.PreviewIsDark;
        set
        {
            if (value)
                _appearance.ApplyPreview(_appearance.PreviewTheme, false);
        }
    }

    public bool IsDarkMode
    {
        get => _appearance.PreviewIsDark;
        set
        {
            if (value)
                _appearance.ApplyPreview(_appearance.PreviewTheme, true);
        }
    }

    public ThemeId CurrentThemeId => _appearance.PreviewTheme;

    [ObservableProperty]
    private string selectedLanguageCode = "en";

    public void LoadStateFromPreferences()
    {
        var code = UiCultureSettings.ResolveUiCulture().TwoLetterISOLanguageName;
        SelectedLanguageCode = code.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        RebuildLanguageOptions();
        OnAppearancePreviewChanged(this, EventArgs.Empty);
    }

    private void OnAppearancePreviewChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsLightMode));
        OnPropertyChanged(nameof(IsDarkMode));
        OnPropertyChanged(nameof(CurrentThemeId));
    }

    private void RebuildLanguageOptions()
    {
        var keep = SelectedLanguageCode;
        LanguageOptions.Clear();
        LanguageOptions.Add(new LanguageOption("de", _loc.GetString("Main_Menu_LanguageGerman")));
        LanguageOptions.Add(new LanguageOption("en", _loc.GetString("Main_Menu_LanguageEnglish")));
        SelectedLanguageCode = keep;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var want = SelectedLanguageCode?.Trim().ToLowerInvariant() is "de" ? "de" : "en";
        var cult = CultureInfo.GetCultureInfo(want);
        if (!string.Equals(
                cult.TwoLetterISOLanguageName,
                _loc.CurrentUiCulture.TwoLetterISOLanguageName,
                StringComparison.OrdinalIgnoreCase))
            _loc.ApplyUiCulture(cult);
        RebuildLanguageOptions();
        _appearance.Persist();
        OnAppearancePreviewChanged(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectAppearanceTheme(ThemeId? themeId)
    {
        if (!themeId.HasValue)
            return;
        _appearance.ApplyPreview(themeId.Value, _appearance.PreviewIsDark);
    }

    /// <summary>Save, discard, or cancel when leaving settings or switching tabs with pending changes.</summary>
    public bool TryConfirmLeavePendingSettings()
    {
        if (!HasUnsavedSettingsChanges)
            return true;

        var result = _dialogs.PromptUnsavedChanges(
            BuildUnsavedSettingsMessage(),
            _loc.GetString("Settings_UnsavedTitle"),
            UnsavedChangesPromptContext.Settings);

        switch (result)
        {
            case UnsavedChangesPromptResult.Save:
                SaveSettings();
                return true;
            case UnsavedChangesPromptResult.Discard:
                DiscardPendingSettings();
                return true;
            default:
                return false;
        }
    }

    private void DiscardPendingSettings()
    {
        SelectedLanguageCode = NormalizeLanguageCode(_loc.CurrentUiCulture.TwoLetterISOLanguageName);
        _appearance.RevertToSaved();
        OnAppearancePreviewChanged(this, EventArgs.Empty);
    }

    private string BuildUnsavedSettingsMessage()
    {
        var fmt = _loc.GetString("Settings_UnsavedChangeFormat");
        var lines = new List<string>();

        if (HasPendingLanguageChange())
        {
            var fromCode = NormalizeLanguageCode(_loc.CurrentUiCulture.TwoLetterISOLanguageName);
            var toCode = NormalizeLanguageCode(SelectedLanguageCode);
            lines.Add(string.Format(_loc.CurrentUiCulture, fmt,
                _loc.GetString("Settings_UnsavedCategoryLanguage"),
                LanguageDisplayName(fromCode),
                LanguageDisplayName(toCode)));
        }

        if (_appearance.PreviewIsDark != _appearance.LastPersistedIsDark)
        {
            lines.Add(string.Format(_loc.CurrentUiCulture, fmt,
                _loc.GetString("Settings_UnsavedCategoryAppearanceMode"),
                AppearanceModeDisplay(_appearance.LastPersistedIsDark),
                AppearanceModeDisplay(_appearance.PreviewIsDark)));
        }

        if (_appearance.PreviewTheme != _appearance.LastPersistedTheme)
        {
            lines.Add(string.Format(_loc.CurrentUiCulture, fmt,
                _loc.GetString("Visuals_ColorThemeHeader"),
                ThemeDisplayName(_appearance.LastPersistedTheme),
                ThemeDisplayName(_appearance.PreviewTheme)));
        }

        var intro = _loc.GetString("Settings_UnsavedIntro");
        var outro = _loc.GetString("Settings_UnsavedOutro");
        if (lines.Count == 0)
            return $"{intro}\n\n{outro}";

        var bullet = string.Join("\n", lines.Select(static l => "• " + l));
        return $"{intro}\n\n{bullet}\n\n{outro}";
    }

    private bool HasPendingLanguageChange() =>
        !string.Equals(
            NormalizeLanguageCode(SelectedLanguageCode),
            NormalizeLanguageCode(_loc.CurrentUiCulture.TwoLetterISOLanguageName),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeLanguageCode(string? code) =>
        code?.Trim().ToLowerInvariant() is "de" ? "de" : "en";

    private string LanguageDisplayName(string code) =>
        NormalizeLanguageCode(code) == "de"
            ? _loc.GetString("Main_Menu_LanguageGerman")
            : _loc.GetString("Main_Menu_LanguageEnglish");

    private static ThemeCatalogEntry? EntryFor(ThemeId id) =>
        ThemeCatalog.Entries.FirstOrDefault(e => e.Id == id);

    private string ThemeDisplayName(ThemeId id)
    {
        var entry = EntryFor(id);
        return entry is null ? id.ToString() : _loc.GetString(entry.DisplayNameKey);
    }

    private string AppearanceModeDisplay(bool isDark) =>
        isDark ? _loc.GetString("Visuals_Dark") : _loc.GetString("Visuals_Light");
}
