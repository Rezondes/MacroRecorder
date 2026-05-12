using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel shellViewModel)
    {
        DataContext = shellViewModel;
        InitializeComponent();
        Loaded += async (_, _) => await shellViewModel.Overview.RefreshAsync().ConfigureAwait(true);
    }

    private ShellViewModel Shell => (ShellViewModel)DataContext;

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!Shell.TryLeaveTopPage())
            e.Cancel = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        if (Shell.IsSettingsOpen)
        {
            Shell.CloseSettingsCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Shell.CanGoBack && Shell.GoBackCommand.CanExecute(null))
        {
            Shell.GoBackCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnSettingsOverlayBackgroundMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, sender))
            return;
        Shell.CloseSettingsCommand.Execute(null);
    }

    private void OnSettingsModalInnerMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;
}
