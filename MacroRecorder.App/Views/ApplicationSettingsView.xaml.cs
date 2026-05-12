using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Localization;
using MacroRecorder.App.Services;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App.Views;

public partial class ApplicationSettingsView : UserControl
{
    private readonly record struct LanguageOption(string Code, string Display);

    public ApplicationSettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => PopulateLanguageCombo();
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

    private ShellViewModel? Shell => Window.GetWindow(this)?.DataContext as ShellViewModel;

    private void OnCancel(object sender, RoutedEventArgs e) => Shell?.CloseSettingsCommand.Execute(null);

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var selected = LanguageCombo.SelectedValue as string;
        if (string.IsNullOrEmpty(selected))
            selected = "en";

        var current = UiCultureSettings.ResolveUiCulture().TwoLetterISOLanguageName;
        if (selected.Equals(current, StringComparison.OrdinalIgnoreCase))
        {
            Shell?.CloseSettingsCommand.Execute(null);
            return;
        }

        UiCultureSettings.SaveUiCulturePreference(selected);
        var loc = UiLocalizerHost.Current;
        if (loc is null)
        {
            Shell?.CloseSettingsCommand.Execute(null);
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

        Shell?.CloseSettingsCommand.Execute(null);
    }
}
