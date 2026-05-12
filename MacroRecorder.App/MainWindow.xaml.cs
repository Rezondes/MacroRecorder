using System.Windows;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await viewModel.RefreshAsync().ConfigureAwait(true);
    }

    private void OnOpenApplicationSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new ApplicationSettingsWindow { Owner = this };
        dialog.ShowDialog();
    }
}
