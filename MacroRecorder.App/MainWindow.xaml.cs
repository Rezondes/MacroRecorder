using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MacroRecorder.App.Services;
using MacroRecorder.App.ViewModels;

namespace MacroRecorder.App;

public partial class MainWindow : Window
{
    private readonly AppearanceService _appearance;
    private readonly EventHandler _onAppearancePreviewChanged;
    private bool _forceClose;

    public MainWindow(ShellViewModel shellViewModel, AppearanceService appearance)
    {
        _appearance = appearance;
        _onAppearancePreviewChanged = (_, _) => Dispatcher.Invoke(ApplyCaptionTheme);
        DataContext = shellViewModel;
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyCaptionTheme();
        Loaded += async (_, _) =>
        {
            ApplyCaptionTheme();
            await shellViewModel.Overview.RefreshAsync().ConfigureAwait(true);
        };
        Closed += (_, _) => _appearance.PreviewChanged -= _onAppearancePreviewChanged;
        _appearance.PreviewChanged += _onAppearancePreviewChanged;
    }
    private ShellViewModel Shell => (ShellViewModel)DataContext;

    private void ApplyCaptionTheme() => CaptionThemeHelper.Apply(this, _appearance.PreviewIsDark);

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)    {
        if (_forceClose)
            return;
        e.Cancel = true;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            if (!await Shell.TryLeaveTopPageAsync().ConfigureAwait(true))
                return;
            _forceClose = true;
            try
            {
                Close();
            }
            finally
            {
                _forceClose = false;
            }
        });
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        if (Shell.IsInfoModalOpen)
        {
            Shell.CloseInfoModalCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Shell.IsContentModalOpen)
        {
            Shell.ContentModalEscapeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Shell.IsConfirmModalOpen)
        {
            Shell.ConfirmModalNoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Shell.IsUnsavedChangesModalOpen)
        {
            Shell.UnsavedChangesModalCancelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Shell.CanGoBack && Shell.GoBackCommand.CanExecute(null))
        {
            Shell.GoBackCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnInfoOverlayBackgroundMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, sender))
            return;
        Shell.CloseInfoModalCommand.Execute(null);
    }

    private void OnInfoModalInnerMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnUnsavedModalInnerMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnConfirmModalInnerMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnContentModalInnerMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;
}
