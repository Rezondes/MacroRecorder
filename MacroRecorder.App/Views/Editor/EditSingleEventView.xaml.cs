using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Input;
using MacroRecorder.Wpf.Controls;

namespace MacroRecorder.App.Views.Editor;

public partial class EditSingleEventView : UserControl, IContentModalEscape
{
    private readonly RecordedInputEvent _original;
    private readonly IUiLocalizer _loc;
    private readonly Action<string> _showValidationError;
    private readonly Action<bool> _onCompleted;
    private readonly Dictionary<string, FrameworkElement> _fields = new();
    private ComboBox? _mouseButtonCombo;

    private ushort _keyVk;
    private ushort _keyScan;
    private TextBlock? _keyNameDisplay;
    private TextBlock? _keyListenHint;
    private WinKeyLowLevelHookScope? _keyHook;
    private bool _awaitingKeyCapture;

    public EditSingleEventView(
        RecordedInputEvent original,
        IUiLocalizer loc,
        Action<string> showValidationError,
        Action<bool> onCompleted)
    {
        _original = original;
        _loc = loc;
        _showValidationError = showValidationError;
        _onCompleted = onCompleted;
        InitializeComponent();
        PreviewKeyDown += OnRootPreviewKeyDown;
        Unloaded += OnUnloaded;
        BuildFields();
    }

    /// <summary>When true, the main window defers Escape / Win handling so physical keys can be captured.</summary>
    public bool IsEditingKeyPhysicalEvent => _original is KeyDownRecordedEvent or KeyUpRecordedEvent;

    public RecordedInputEvent? ResultEvent { get; private set; }

    public void CancelFromHost() => _onCompleted(false);

    private void OnCancelClick(object sender, RoutedEventArgs e) => _onCompleted(false);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ResultEvent = _original switch
            {
                KeyDownRecordedEvent keyDown => keyDown with
                {
                    Vk = _keyVk,
                    ScanCode = _keyScan,
                    IsExtendedKey = Bool("ext"),
                    IsAltDown = Bool("alt"),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                KeyUpRecordedEvent keyUp => keyUp with
                {
                    Vk = _keyVk,
                    ScanCode = _keyScan,
                    IsExtendedKey = Bool("ext"),
                    IsAltDown = Bool("alt"),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                MouseMoveRecordedEvent mouseMove => mouseMove with
                {
                    ScreenX = Int("x"),
                    ScreenY = Int("y"),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                MouseButtonDownRecordedEvent mouseButtonDown => mouseButtonDown with
                {
                    Button = ReadSelectedMouseButton(),
                    ScreenX = Int("x"),
                    ScreenY = Int("y"),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with
                {
                    Button = ReadSelectedMouseButton(),
                    ScreenX = Int("x"),
                    ScreenY = Int("y"),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                MouseWheelRecordedEvent mouseWheel => mouseWheel with
                {
                    ScreenX = Int("x"),
                    ScreenY = Int("y"),
                    WheelDelta = Int("delta"),
                    IsHorizontal = Bool("horiz"),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                SyntheticWaitRecordedEvent syntheticWait => syntheticWait with
                {
                    AdditionalDelay = TimeSpan.FromMilliseconds(Double("ms")),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                FocusChangedRecordedEvent focusChanged => focusChanged with
                {
                    WindowTitle = Str("title"),
                    ProcessName = Str("proc"),
                    Hwnd = ULongOrNull("hwnd"),
                    ProcessId = UIntOrNull("pid"),
                    ReferenceClientWidth = IntOrNull("refW"),
                    ReferenceClientHeight = IntOrNull("refH"),
                    DelayBefore = ReadDelayBeforeFromField()
                },
                _ => null
            };
        }
        catch (Exception exception)
        {
            _showValidationError(exception.Message);
            return;
        }

        if (ResultEvent is null)
            return;

        _onCompleted(true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _keyHook?.Dispose();
        _keyHook = null;
    }

    private void AddField(string label, string fieldKey, string value, bool restrictToDigitsOnlyCoordinates = false)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 4)
        };
        labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextSecondary");
        FieldsPanel.Children.Add(labelBlock);

        if (restrictToDigitsOnlyCoordinates)
        {
            var box = new DigitsOnlyNumericBox
            {
                Text = value,
                Tag = fieldKey,
                DigitsOnly = true,
                ShowSpinner = false,
                InputFontSize = 13,
                MinInnerHeight = 36,
                MinHeight = 36
            };
            FieldsPanel.Children.Add(box);
            _fields[fieldKey] = box;
            return;
        }

        var textBox = new TextBox
        {
            Text = value,
            Tag = fieldKey,
            FontSize = 13,
            MinHeight = 36,
            Padding = new Thickness(10, 6, 10, 6),
            BorderThickness = new Thickness(1)
        };
        textBox.SetResourceReference(TextBox.BackgroundProperty, "UiBrush.SurfaceElevated");
        textBox.SetResourceReference(TextBox.ForegroundProperty, "UiBrush.TextPrimary");
        textBox.SetResourceReference(TextBox.BorderBrushProperty, "UiBrush.Border");
        FieldsPanel.Children.Add(textBox);
        _fields[fieldKey] = textBox;
    }

    private void AddBoolCheckboxField(string label, string fieldKey, bool value)
    {
        var checkBox = new CheckBox
        {
            Content = label,
            Tag = fieldKey,
            FontSize = 13,
            Margin = new Thickness(0, 10, 0, 0),
            IsChecked = value
        };
        checkBox.SetResourceReference(Control.ForegroundProperty, "UiBrush.TextPrimary");
        FieldsPanel.Children.Add(checkBox);
        _fields[fieldKey] = checkBox;
    }

    private void AddCoordinateFieldWithSpinner(string label, string fieldKey, string value)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 4)
        };
        labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextSecondary");
        FieldsPanel.Children.Add(labelBlock);

        var box = new DigitsOnlyNumericBox
        {
            Text = value,
            Tag = fieldKey,
            DigitsOnly = true,
            ShowSpinner = true,
            InputFontSize = 13,
            MinInnerHeight = 40,
            SpinnerStep = 1,
            MinimumValue = int.MinValue,
            MaximumValue = int.MaxValue,
            SpinUpToolTip = _loc.GetString("Editor_PromptWaitSpinUp"),
            SpinDownToolTip = _loc.GetString("Editor_PromptWaitSpinDown")
        };
        FieldsPanel.Children.Add(box);
        _fields[fieldKey] = box;
    }

    private void AddWaitMillisecondsField(string label, string fieldKey, string value, int minimumValue = 1)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 4)
        };
        labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextSecondary");
        FieldsPanel.Children.Add(labelBlock);

        var box = new DigitsOnlyNumericBox
        {
            Text = value,
            Tag = fieldKey,
            DigitsOnly = true,
            ShowSpinner = true,
            InputFontSize = 13,
            MinInnerHeight = 40,
            SpinnerStep = 1,
            MinimumValue = minimumValue,
            MaximumValue = int.MaxValue,
            SpinUpToolTip = _loc.GetString("Editor_PromptWaitSpinUp"),
            SpinDownToolTip = _loc.GetString("Editor_PromptWaitSpinDown")
        };
        FieldsPanel.Children.Add(box);
        _fields[fieldKey] = box;
    }

    private void AddDelayBeforeMillisecondsField()
    {
        var msText = Math.Round(_original.DelayBefore.TotalMilliseconds).ToString("0", CultureInfo.InvariantCulture);
        AddWaitMillisecondsField(_loc.GetString("DialogEdit_Field_DelayBeforeMs"), "delayBefore", msText, minimumValue: 0);
    }

    private TimeSpan ReadDelayBeforeFromField()
    {
        var trimmed = ReadFieldText("delayBefore").Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new FormatException(_loc.GetString("Editor_PromptWaitErrorRequired"));
        var ms = double.Parse(trimmed, CultureInfo.InvariantCulture);
        if (ms < 0)
            ms = 0;
        return TimeSpan.FromMilliseconds(ms);
    }

    private void AddMouseButtonField(string label, MouseButtonKind selected)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 4)
        };
        labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextSecondary");
        FieldsPanel.Children.Add(labelBlock);
        var combo = new ComboBox
        {
            FontSize = 13,
            MinHeight = 40,
            Margin = new Thickness(0, 0, 0, 12)
        };
        combo.SetResourceReference(FrameworkElement.StyleProperty, "UiComboBox");
        combo.ItemsSource = Enum.GetValues<MouseButtonKind>();
        combo.SelectedItem = selected;
        FieldsPanel.Children.Add(combo);
        _mouseButtonCombo = combo;
    }

    private void AddKeyboardKeySection(ushort vk, ushort scanCode, bool isExtendedKey, bool isAltDown)
    {
        _keyVk = vk;
        _keyScan = scanCode;

        var keyHeading = new TextBlock
        {
            Text = _loc.GetString("DialogEdit_Field_KeyCurrent"),
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 4)
        };
        keyHeading.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextSecondary");
        FieldsPanel.Children.Add(keyHeading);

        _keyNameDisplay = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        _keyNameDisplay.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextPrimary");
        FieldsPanel.Children.Add(_keyNameDisplay);

        var captureButton = new Button
        {
            Content = _loc.GetString("DialogEdit_RescanKey"),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        captureButton.SetResourceReference(FrameworkElement.StyleProperty, "UiButtonChrome");
        captureButton.Click += (_, _) => StartKeyCaptureListen();
        FieldsPanel.Children.Add(captureButton);

        _keyListenHint = new TextBlock
        {
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        _keyListenHint.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextSecondary");
        FieldsPanel.Children.Add(_keyListenHint);

        AddBoolCheckboxField(_loc.GetString("DialogEdit_Field_Extended"), "ext", isExtendedKey);
        AddBoolCheckboxField(_loc.GetString("DialogEdit_Field_AltDown"), "alt", isAltDown);

        if (_fields["ext"] is CheckBox extendedCheck)
        {
            extendedCheck.Checked += (_, _) => RefreshKeyDisplayName();
            extendedCheck.Unchecked += (_, _) => RefreshKeyDisplayName();
        }

        RefreshKeyDisplayName();
    }

    private void RefreshKeyDisplayName()
    {
        if (_keyNameDisplay is null)
            return;
        var extended = Bool("ext");
        _keyNameDisplay.Text = KeyDisplayName.GetName(_keyVk, _keyScan, extended);
    }

    private void StartKeyCaptureListen()
    {
        StopKeyCaptureListen();
        _awaitingKeyCapture = true;
        if (_keyListenHint is not null)
        {
            _keyListenHint.Text = _loc.GetString("DialogInsertKey_PressKeyHint");
            _keyListenHint.Visibility = Visibility.Visible;
        }

        Focus();
        Keyboard.Focus(this);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, InstallKeyHook);
    }

    private void StopKeyCaptureListen()
    {
        _awaitingKeyCapture = false;
        if (_keyListenHint is not null)
            _keyListenHint.Visibility = Visibility.Collapsed;
        _keyHook?.Dispose();
        _keyHook = null;
    }

    private void InstallKeyHook()
    {
        if (!_awaitingKeyCapture)
            return;
        _keyHook?.Dispose();
        _keyHook = null;
        var w = Window.GetWindow(this);
        if (w is null)
            return;
        var handle = new WindowInteropHelper(w).Handle;
        if (handle == nint.Zero)
            return;
        _keyHook = new WinKeyLowLevelHookScope(handle, OnWinKeyPhysicalFromHook);
    }

    private void OnWinKeyPhysicalFromHook(uint vkCode, ushort scanFromHook, bool extendedFromHook)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!_awaitingKeyCapture)
                return;
            var physicalKey = KeyInterop.KeyFromVirtualKey((int)vkCode);
            var scan = scanFromHook == 0 ? null : (ushort?)scanFromHook;
            ApplyCapturedKey(physicalKey, scan, extendedFromHook);
        });
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
    {
        if (!_awaitingKeyCapture || keyEventArgs.IsRepeat)
            return;
        var physicalKey = keyEventArgs.Key == Key.System ? keyEventArgs.SystemKey : keyEventArgs.Key;
        ApplyCapturedKey(physicalKey, null, false);
        keyEventArgs.Handled = true;
    }

    private void ApplyCapturedKey(Key physicalKey, ushort? scanOverride, bool extendedFromHook)
    {
        if (physicalKey is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        {
            if (_keyListenHint is not null)
                _keyListenHint.Text = _loc.GetString("DialogKeyStroke_ModifierHint", physicalKey);
        }

        _keyVk = (ushort)KeyInterop.VirtualKeyFromKey(physicalKey);
        _keyScan = scanOverride ?? VkScanMapper.VirtualKeyToScanCode(_keyVk);
        var extended = extendedFromHook || IsExtendedKey(physicalKey);
        if (_fields.TryGetValue("ext", out var extEl) && extEl is CheckBox extCb)
            extCb.IsChecked = extended;

        RefreshKeyDisplayName();
        StopKeyCaptureListen();
    }

    private static bool IsExtendedKey(Key key)
    {
        return key is Key.Insert or Key.Delete or Key.Home or Key.End or Key.Prior or Key.Next
            or Key.Up or Key.Down or Key.Left or Key.Right
            or Key.NumLock or Key.Divide
            or Key.RWin or Key.LWin or Key.RightAlt or Key.RightCtrl;
    }

    private MouseButtonKind ReadSelectedMouseButton() =>
        _mouseButtonCombo?.SelectedItem is MouseButtonKind kind ? kind : MouseButtonKind.Left;

    private void BuildFields()
    {
        _mouseButtonCombo = null;
        switch (_original)
        {
            case KeyDownRecordedEvent keyDown:
                AddKeyboardKeySection(keyDown.Vk, keyDown.ScanCode, keyDown.IsExtendedKey, keyDown.IsAltDown);
                AddDelayBeforeMillisecondsField();
                break;
            case KeyUpRecordedEvent keyUp:
                AddKeyboardKeySection(keyUp.Vk, keyUp.ScanCode, keyUp.IsExtendedKey, keyUp.IsAltDown);
                AddDelayBeforeMillisecondsField();
                break;
            case MouseMoveRecordedEvent mouseMove:
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseMove.ScreenX.ToString(), restrictToDigitsOnlyCoordinates: true);
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseMove.ScreenY.ToString(), restrictToDigitsOnlyCoordinates: true);
                AddDelayBeforeMillisecondsField();
                break;
            case MouseButtonDownRecordedEvent mouseButtonDown:
                AddMouseButtonField(_loc.GetString("DialogEdit_Field_Button"), mouseButtonDown.Button);
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_X"), "x", mouseButtonDown.ScreenX.ToString());
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_Y"), "y", mouseButtonDown.ScreenY.ToString());
                AddDelayBeforeMillisecondsField();
                break;
            case MouseButtonUpRecordedEvent mouseButtonUp:
                AddMouseButtonField(_loc.GetString("DialogEdit_Field_Button"), mouseButtonUp.Button);
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_X"), "x", mouseButtonUp.ScreenX.ToString());
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_Y"), "y", mouseButtonUp.ScreenY.ToString());
                AddDelayBeforeMillisecondsField();
                break;
            case MouseWheelRecordedEvent mouseWheel:
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseWheel.ScreenX.ToString(), restrictToDigitsOnlyCoordinates: true);
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseWheel.ScreenY.ToString(), restrictToDigitsOnlyCoordinates: true);
                AddField(_loc.GetString("DialogEdit_Field_Delta"), "delta", mouseWheel.WheelDelta.ToString());
                AddBoolCheckboxField(_loc.GetString("DialogEdit_Field_Horizontal"), "horiz", mouseWheel.IsHorizontal);
                AddDelayBeforeMillisecondsField();
                break;
            case SyntheticWaitRecordedEvent syntheticWait:
                AddWaitMillisecondsField(
                    _loc.GetString("DialogEdit_Field_WaitMs"),
                    "ms",
                    syntheticWait.AdditionalDelay.TotalMilliseconds.ToString("0"));
                AddDelayBeforeMillisecondsField();
                break;
            case FocusChangedRecordedEvent focusChanged:
                AddField(_loc.GetString("DialogEdit_Field_WindowTitle"), "title", focusChanged.WindowTitle);
                AddField(_loc.GetString("DialogEdit_Field_ProcessName"), "proc", focusChanged.ProcessName);
                AddField(_loc.GetString("DialogEdit_Field_Hwnd"), "hwnd", focusChanged.Hwnd?.ToString() ?? "");
                AddField(_loc.GetString("DialogEdit_Field_Pid"), "pid", focusChanged.ProcessId?.ToString() ?? "");
                AddField(
                    _loc.GetString("DialogEdit_Field_ReferenceClientWidth"),
                    "refW",
                    focusChanged.ReferenceClientWidth?.ToString(CultureInfo.InvariantCulture) ?? "");
                AddField(
                    _loc.GetString("DialogEdit_Field_ReferenceClientHeight"),
                    "refH",
                    focusChanged.ReferenceClientHeight?.ToString(CultureInfo.InvariantCulture) ?? "");
                AddDelayBeforeMillisecondsField();
                break;
            default:
                var notEditable = new TextBlock
                {
                    Text = _loc.GetString("DialogEdit_EventNotEditable"),
                    TextWrapping = TextWrapping.Wrap
                };
                notEditable.SetResourceReference(TextBlock.ForegroundProperty, "UiBrush.TextPrimary");
                FieldsPanel.Children.Add(notEditable);
                break;
        }
    }

    private string Str(string fieldKey) => ReadFieldText(fieldKey).Trim();

    private string ReadFieldText(string fieldKey)
    {
        if (!_fields.TryGetValue(fieldKey, out var element))
            return string.Empty;
        return element switch
        {
            DigitsOnlyNumericBox digitsBox => digitsBox.Text,
            TextBox textBox => textBox.Text,
            CheckBox checkBox => checkBox.IsChecked == true ? "True" : "False",
            _ => string.Empty
        };
    }

    private int Int(string fieldKey) => int.Parse(Str(fieldKey), CultureInfo.InvariantCulture);

    private double Double(string fieldKey) =>
        double.Parse(Str(fieldKey), CultureInfo.InvariantCulture);

    private bool Bool(string fieldKey)
    {
        if (_fields.TryGetValue(fieldKey, out var element) && element is CheckBox checkBox)
            return checkBox.IsChecked == true;
        return bool.Parse(Str(fieldKey));
    }

    private ulong? ULongOrNull(string fieldKey)
    {
        var trimmedText = Str(fieldKey);
        return string.IsNullOrEmpty(trimmedText) ? null : ulong.Parse(trimmedText, CultureInfo.InvariantCulture);
    }

    private uint? UIntOrNull(string fieldKey)
    {
        var trimmedText = Str(fieldKey);
        return string.IsNullOrEmpty(trimmedText) ? null : uint.Parse(trimmedText, CultureInfo.InvariantCulture);
    }

    private int? IntOrNull(string fieldKey)
    {
        var trimmedText = Str(fieldKey);
        return string.IsNullOrEmpty(trimmedText) ? null : int.Parse(trimmedText, CultureInfo.InvariantCulture);
    }
}
