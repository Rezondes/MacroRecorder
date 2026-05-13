using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MacroRecorder.App.Services;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.App;

public partial class MainWindow : Window
{
    private const double MinPersistWidth = 640;
    private const double MinPersistHeight = 400;

    private readonly AppearanceService _appearance;
    private readonly MacroPlaybackHotkeyRegistrar _playbackHotkeys;
    private readonly EventHandler _onAppearancePreviewChanged;
    private readonly DispatcherTimer _windowPlacementSaveDebounce;
    private bool _forceClose;
    private HwndSource? _hwndSource;

    public MainWindow(ShellViewModel shellViewModel, AppearanceService appearance, MacroPlaybackHotkeyRegistrar playbackHotkeys)
    {
        _appearance = appearance;
        _playbackHotkeys = playbackHotkeys;
        _onAppearancePreviewChanged = (_, _) => Dispatcher.Invoke(ApplyCaptionTheme);
        DataContext = shellViewModel;
        InitializeComponent();
        ApplyPersistedWindowPlacement();
        _windowPlacementSaveDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400),
            IsEnabled = false
        };
        _windowPlacementSaveDebounce.Tick += (_, _) =>
        {
            _windowPlacementSaveDebounce.Stop();
            PersistWindowPlacementNow();
        };
        LocationChanged += (_, _) => SchedulePersistWindowPlacement();
        SizeChanged += (_, _) => SchedulePersistWindowPlacement();
        SourceInitialized += OnMainWindowSourceInitialized;
        Loaded += async (_, _) =>
        {
            ApplyCaptionTheme();
            await shellViewModel.Overview.RefreshAsync(suppressHotkeyRegistrationFailureDialog: true).ConfigureAwait(true);
        };
        Closed += (_, _) =>
        {
            _windowPlacementSaveDebounce.Stop();
            PersistWindowPlacementNow();
            _appearance.PreviewChanged -= _onAppearancePreviewChanged;
        };
        _appearance.PreviewChanged += _onAppearancePreviewChanged;
    }

    private ShellViewModel Shell => (ShellViewModel)DataContext;

    private void OnMainWindowSourceInitialized(object? sender, EventArgs e)
    {
        ApplyCaptionTheme();
        var handle = new WindowInteropHelper(this).Handle;
        _playbackHotkeys.AttachWindow(handle);
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == User32Hotkeys.WmHotkey && _playbackHotkeys.TryConsumeWmHotKey(wParam))
            handled = true;
        return nint.Zero;
    }

    private void ApplyCaptionTheme() => CaptionThemeHelper.Apply(this, _appearance.PreviewIsDark);

    private void ApplyPersistedWindowPlacement()
    {
        var placement = AppSettingsStore.Load().MainWindowPlacement;
        if (placement is null || !IsPlacementOnVirtualScreen(placement))
            return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
    }

    private static bool IsPlacementOnVirtualScreen(MainWindowPlacement placement)
    {
        if (placement.Width < MinPersistWidth || placement.Height < MinPersistHeight)
            return false;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        var margin = 64;
        if (placement.Left + placement.Width < virtualLeft + margin ||
            placement.Left > virtualRight - margin ||
            placement.Top + placement.Height < virtualTop + margin ||
            placement.Top > virtualBottom - margin)
            return false;
        return true;
    }

    private void SchedulePersistWindowPlacement()
    {
        if (!IsLoaded)
            return;
        _windowPlacementSaveDebounce.Stop();
        _windowPlacementSaveDebounce.Start();
    }

    private void PersistWindowPlacementNow()
    {
        if (!TryGetCurrentPlacementToSave(out var placement))
            return;
        AppSettingsStore.SaveMainWindowPlacementOnly(placement);
    }

    private bool TryGetCurrentPlacementToSave(out MainWindowPlacement placement)
    {
        placement = default!;
        if (WindowState == WindowState.Minimized)
            return false;

        double left;
        double top;
        double width;
        double height;
        if (WindowState == WindowState.Normal)
        {
            left = Left;
            top = Top;
            width = ActualWidth;
            height = ActualHeight;
        }
        else
        {
            var restore = RestoreBounds;
            left = restore.Left;
            top = restore.Top;
            width = restore.Width;
            height = restore.Height;
        }

        if (width < MinPersistWidth - 0.5 || height < MinPersistHeight - 0.5)
            return false;
        if (double.IsNaN(left) || double.IsNaN(top) || double.IsInfinity(left) || double.IsInfinity(top))
            return false;

        placement = new MainWindowPlacement
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height
        };
        return true;
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
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

    private static bool ContentModalDefersPhysicalKeyRouting(object? content) =>
        content is Views.Editor.EditSingleEventView editEventView && editEventView.IsEditingKeyPhysicalEvent;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Shell.IsContentModalOpen && ContentModalDefersPhysicalKeyRouting(Shell.ContentModalContent))
        {
            var physicalKey = e.Key == Key.System ? e.SystemKey : e.Key;
            if (e.Key == Key.Escape || physicalKey is Key.LWin or Key.RWin)
                return;
        }

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
