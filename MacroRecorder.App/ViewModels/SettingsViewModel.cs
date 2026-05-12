using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
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

    /// <summary>0 = General, 1 = Visuals. TwoWay-bound; leaving Visuals with dirty appearance prompts before the index changes.</summary>
    public int SelectedSettingsTabIndex
    {
        get => _selectedSettingsTabIndex;
        set
        {
            if (value == _selectedSettingsTabIndex)
                return;

            if (_selectedSettingsTabIndex == 1 && value != 1 && _appearance.HasPendingChanges)
            {
                if (!TryConfirmLeaveAppearance())
                {
                    OnPropertyChanged(nameof(SelectedSettingsTabIndex));
                    var dispatcher = global::System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher is not null && !dispatcher.HasShutdownStarted)
                    {
                        dispatcher.BeginInvoke(
                            new Action(() => OnPropertyChanged(nameof(SelectedSettingsTabIndex))),
                            DispatcherPriority.Input);
                    }

                    return;
                }
            }

            SetProperty(ref _selectedSettingsTabIndex, value);
        }
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

    /// <summary>Used when leaving the Visuals tab or the settings page with unsaved appearance.</summary>
    public bool TryConfirmLeaveAppearance()
    {
        if (!_appearance.HasPendingChanges)
            return true;
        var result = _dialogs.PromptUnsavedChanges(
            _loc.GetString("Appearance_UnsavedMessage"),
            _loc.GetString("Appearance_UnsavedTitle"),
            UnsavedChangesPromptContext.Appearance);
        switch (result)
        {
            case UnsavedChangesPromptResult.Save:
                _appearance.Persist();
                OnAppearancePreviewChanged(this, EventArgs.Empty);
                return true;
            case UnsavedChangesPromptResult.Discard:
                _appearance.RevertToSaved();
                return true;
            default:
                return false;
        }
    }
}
