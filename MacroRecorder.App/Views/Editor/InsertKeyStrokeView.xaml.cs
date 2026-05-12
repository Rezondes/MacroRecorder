using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Input;

namespace MacroRecorder.App.Views.Editor;

public partial class InsertKeyStrokeView : UserControl, IContentModalKeyCaptureHost
{
    private readonly IUiLocalizer _loc;
    private readonly Action<string> _showValidation;
    private readonly Action<bool> _onCompleted;
    private ushort _capturedVirtualKey;
    private ushort _capturedScanCode;
    private bool _isExtendedKey;
    private WinKeyLowLevelHookScope? _winKeyHook;

    public InsertKeyStrokeView(IUiLocalizer loc, Action<string> showValidation, Action<bool> onCompleted)
    {
        _loc = loc;
        _showValidation = showValidation;
        _onCompleted = onCompleted;
        InitializeComponent();
        StatusText.Text = _loc.GetString("DialogInsertKey_PressKeyHint");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, InstallWinKeyHook);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _winKeyHook?.Dispose();
        _winKeyHook = null;
    }

    private void InstallWinKeyHook()
    {
        _winKeyHook?.Dispose();
        _winKeyHook = null;
        var w = Window.GetWindow(this);
        if (w is null)
            return;
        var handle = new WindowInteropHelper(w).Handle;
        if (handle == nint.Zero)
            return;
        _winKeyHook = new WinKeyLowLevelHookScope(handle, OnWinKeyPhysicalFromHook);
    }

    /// <summary>Low-level hook runs outside WPF routing; marshal to UI thread so capture matches <see cref="OnPreviewKeyDown"/>.</summary>
    private void OnWinKeyPhysicalFromHook(uint vkCode, ushort scanFromHook, bool extendedFromHook)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var physicalKey = KeyInterop.KeyFromVirtualKey((int)vkCode);
            var scan = scanFromHook == 0 ? null : (ushort?)scanFromHook;
            ApplyCaptured(physicalKey, scan, extendedFromHook);
        });
    }

    private void ApplyCaptured(Key physicalKey, ushort? scanOverride, bool extendedFromHook)
    {
        if (physicalKey is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        {
            StatusText.Text = _loc.GetString("DialogKeyStroke_ModifierHint", physicalKey);
        }

        _capturedVirtualKey = (ushort)KeyInterop.VirtualKeyFromKey(physicalKey);
        _capturedScanCode = scanOverride ?? VkScanMapper.VirtualKeyToScanCode(_capturedVirtualKey);
        _isExtendedKey = extendedFromHook || IsExtendedKey(physicalKey);
        StatusText.Text = KeyDisplayName.GetName(_capturedVirtualKey, _capturedScanCode, _isExtendedKey);
    }

    public ushort CapturedVk => _capturedVirtualKey;

    public ushort CapturedScan => _capturedScanCode;

    public bool CapturedExtended => _isExtendedKey;

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs.IsRepeat)
            return;
        var physicalKey = keyEventArgs.Key == Key.System ? keyEventArgs.SystemKey : keyEventArgs.Key;
        ApplyCaptured(physicalKey, null, false);
        keyEventArgs.Handled = true;
    }

    private static bool IsExtendedKey(Key key)
    {
        return key is Key.Insert or Key.Delete or Key.Home or Key.End or Key.Prior or Key.Next
            or Key.Up or Key.Down or Key.Left or Key.Right
            or Key.NumLock or Key.Divide
            or Key.RWin or Key.LWin or Key.RightAlt or Key.RightCtrl;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (_capturedVirtualKey == 0 && _capturedScanCode == 0)
        {
            _showValidation(_loc.GetString("DialogKeyStroke_EnterKeyFirst"));
            return;
        }

        _onCompleted(true);
    }
}
