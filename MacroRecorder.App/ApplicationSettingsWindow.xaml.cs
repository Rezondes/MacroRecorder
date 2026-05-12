using System.Diagnostics;
using System.Windows;
using MacroRecorder.App.Localization;
using MacroRecorder.App.Services;

namespace MacroRecorder.App;

public partial class ApplicationSettingsWindow : Window
{
    public ApplicationSettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var culture = UiCultureSettings.ResolveUiCulture();
        if (culture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase))
            RbGerman.IsChecked = true;
        else
            RbEnglish.IsChecked = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var selected = RbGerman.IsChecked == true ? "de" : "en";
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
