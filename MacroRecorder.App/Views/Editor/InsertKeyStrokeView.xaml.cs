using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Input;

namespace MacroRecorder.App.Views.Editor;

public partial class InsertKeyStrokeView : UserControl, IContentModalEscape
{
    private readonly IUiLocalizer _loc;
    private readonly Action<string> _showValidation;
    private readonly Action<bool> _onCompleted;
    private ushort _capturedVirtualKey;
    private ushort _capturedScanCode;
    private bool _isExtendedKey;

    public InsertKeyStrokeView(IUiLocalizer loc, Action<string> showValidation, Action<bool> onCompleted)
    {
        _loc = loc;
        _showValidation = showValidation;
        _onCompleted = onCompleted;
        InitializeComponent();
        StatusText.Text = _loc.GetString("DialogInsertKey_PressKeyHint");
        Loaded += (_, _) =>
        {
            Focus();
            Keyboard.Focus(this);
        };
    }

    public ushort CapturedVk => _capturedVirtualKey;

    public ushort CapturedScan => _capturedScanCode;

    public bool CapturedExtended => _isExtendedKey;

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs.IsRepeat)
            return;
        var physicalKey = keyEventArgs.Key == Key.System ? keyEventArgs.SystemKey : keyEventArgs.Key;
        if (physicalKey is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        {
            StatusText.Text = _loc.GetString("DialogKeyStroke_ModifierHint", physicalKey);
        }

        _capturedVirtualKey = (ushort)KeyInterop.VirtualKeyFromKey(physicalKey);
        _capturedScanCode = VkScanMapper.VirtualKeyToScanCode(_capturedVirtualKey);
        _isExtendedKey = IsExtendedKey(physicalKey);
        StatusText.Text = _loc.GetString(
            "DialogKeyStroke_StatusFormat",
            _capturedVirtualKey,
            _capturedScanCode,
            _isExtendedKey ? _loc.GetString("DialogKeyStroke_StatusExtended") : "");
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
