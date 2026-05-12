using System.Diagnostics;
using System.Windows;
using MacroRecorder.App.Localization;
using MacroRecorder.App.Services;

namespace MacroRecorder.App;

public partial class ApplicationSettingsWindow : Window
{
    private readonly record struct LanguageOption(string Code, string Display);

    public ApplicationSettingsWindow()
    {
        InitializeComponent();
        PopulateLanguageCombo();
    }

    private void PopulateLanguageCombo()
    {
        var loc = UiLocalizerHost.Current;
        var deLabel = loc?.GetString("Main_Menu_LanguageGerman") ?? "Deutsch";
        var enLabel = loc?.GetString("Main_Menu_LanguageEnglish") ?? "English";
        LanguageCombo.ItemsSource = new[]
        {
            new LanguageOption("de", deLabel),
            new LanguageOption("en", enLabel),
        };
        LanguageCombo.DisplayMemberPath = nameof(LanguageOption.Display);
        LanguageCombo.SelectedValuePath = nameof(LanguageOption.Code);

        var code = UiCultureSettings.ResolveUiCulture().TwoLetterISOLanguageName;
        LanguageCombo.SelectedValue = code.Equals("de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var selected = LanguageCombo.SelectedValue as string;
        if (string.IsNullOrEmpty(selected))
            selected = "en";

        var current = UiCultureSettings.ResolveUiCulture().TwoLetterISOLanguageName;
        if (selected.Equals(current, StringComparison.OrdinalIgnoreCase))
        {
            DialogResult = true;
            Close();
            return;
        }

        UiCultureSettings.SaveUiCulturePreference(selected);
        var loc = UiLocalizerHost.Current;
        if (loc is null)
        {
            DialogResult = true;
            Close();
            return;
        }

        if (MessageBox.Show(
                loc.GetString("Main_LanguageRestartQuestion"),
                loc.GetString("Common_AppTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            System.Windows.Application.Current.Shutdown();
            return;
        }

        DialogResult = true;
        Close();
    }
}
