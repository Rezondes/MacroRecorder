using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Input;

namespace MacroRecorder.App.Views.Editor;

public partial class InsertKeyStrokeDialog : Window
{
    private readonly IUiLocalizer _loc;
    private ushort _capturedVirtualKey;
    private ushort _capturedScanCode;
    private bool _isExtendedKey;

    public InsertKeyStrokeDialog(IUiLocalizer loc)
    {
        _loc = loc;
        InitializeComponent();
        StatusText.Text = _loc.GetString("DialogInsertKey_PressKeyHint");
        Focusable = true;
        Loaded += (_, _) => Focus();
    }

    public ushort CapturedVk => _capturedVirtualKey;

    public ushort CapturedScan => _capturedScanCode;

    public bool CapturedExtended => _isExtendedKey;

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

    private void OnOk(object sender, RoutedEventArgs routedEventArgs)
    {
        if (_capturedVirtualKey == 0 && _capturedScanCode == 0)
        {
            MessageBox.Show(this, _loc.GetString("DialogKeyStroke_EnterKeyFirst"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
