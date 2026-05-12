using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
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
        BuildFields();
    }

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
                    Vk = UShort("vk"),
                    ScanCode = UShort("scan"),
                    IsExtendedKey = Bool("ext"),
                    IsAltDown = Bool("alt")
                },
                KeyUpRecordedEvent keyUp => keyUp with
                {
                    Vk = UShort("vk"),
                    ScanCode = UShort("scan"),
                    IsExtendedKey = Bool("ext"),
                    IsAltDown = Bool("alt")
                },
                MouseMoveRecordedEvent mouseMove => mouseMove with
                {
                    ScreenX = Int("x"),
                    ScreenY = Int("y")
                },
                MouseButtonDownRecordedEvent mouseButtonDown => mouseButtonDown with
                {
                    Button = ReadSelectedMouseButton(),
                    ScreenX = Int("x"),
                    ScreenY = Int("y")
                },
                MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with
                {
                    Button = ReadSelectedMouseButton(),
                    ScreenX = Int("x"),
                    ScreenY = Int("y")
                },
                MouseWheelRecordedEvent mouseWheel => mouseWheel with
                {
                    ScreenX = Int("x"),
                    ScreenY = Int("y"),
                    WheelDelta = Int("delta"),
                    IsHorizontal = Bool("horiz")
                },
                SyntheticWaitRecordedEvent syntheticWait => syntheticWait with
                {
                    AdditionalDelay = TimeSpan.FromMilliseconds(Double("ms"))
                },
                FocusChangedRecordedEvent focusChanged => focusChanged with
                {
                    WindowTitle = Str("title"),
                    ProcessName = Str("proc"),
                    Hwnd = ULongOrNull("hwnd"),
                    ProcessId = UIntOrNull("pid")
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

    private MouseButtonKind ReadSelectedMouseButton() =>
        _mouseButtonCombo?.SelectedItem is MouseButtonKind kind ? kind : MouseButtonKind.Left;

    private void BuildFields()
    {
        _mouseButtonCombo = null;
        switch (_original)
        {
            case KeyDownRecordedEvent keyDown:
                AddField(_loc.GetString("DialogEdit_Field_Vk"), "vk", keyDown.Vk.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Scan"), "scan", keyDown.ScanCode.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Extended"), "ext", keyDown.IsExtendedKey.ToString());
                AddField(_loc.GetString("DialogEdit_Field_AltDown"), "alt", keyDown.IsAltDown.ToString());
                break;
            case KeyUpRecordedEvent keyUp:
                AddField(_loc.GetString("DialogEdit_Field_Vk"), "vk", keyUp.Vk.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Scan"), "scan", keyUp.ScanCode.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Extended"), "ext", keyUp.IsExtendedKey.ToString());
                AddField(_loc.GetString("DialogEdit_Field_AltDown"), "alt", keyUp.IsAltDown.ToString());
                break;
            case MouseMoveRecordedEvent mouseMove:
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseMove.ScreenX.ToString(), restrictToDigitsOnlyCoordinates: true);
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseMove.ScreenY.ToString(), restrictToDigitsOnlyCoordinates: true);
                break;
            case MouseButtonDownRecordedEvent mouseButtonDown:
                AddMouseButtonField(_loc.GetString("DialogEdit_Field_Button"), mouseButtonDown.Button);
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_X"), "x", mouseButtonDown.ScreenX.ToString());
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_Y"), "y", mouseButtonDown.ScreenY.ToString());
                break;
            case MouseButtonUpRecordedEvent mouseButtonUp:
                AddMouseButtonField(_loc.GetString("DialogEdit_Field_Button"), mouseButtonUp.Button);
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_X"), "x", mouseButtonUp.ScreenX.ToString());
                AddCoordinateFieldWithSpinner(_loc.GetString("DialogEdit_Field_Y"), "y", mouseButtonUp.ScreenY.ToString());
                break;
            case MouseWheelRecordedEvent mouseWheel:
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseWheel.ScreenX.ToString(), restrictToDigitsOnlyCoordinates: true);
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseWheel.ScreenY.ToString(), restrictToDigitsOnlyCoordinates: true);
                AddField(_loc.GetString("DialogEdit_Field_Delta"), "delta", mouseWheel.WheelDelta.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Horizontal"), "horiz", mouseWheel.IsHorizontal.ToString());
                break;
            case SyntheticWaitRecordedEvent syntheticWait:
                AddField(_loc.GetString("DialogEdit_Field_WaitMs"), "ms", syntheticWait.AdditionalDelay.TotalMilliseconds.ToString("0"));
                break;
            case FocusChangedRecordedEvent focusChanged:
                AddField(_loc.GetString("DialogEdit_Field_WindowTitle"), "title", focusChanged.WindowTitle);
                AddField(_loc.GetString("DialogEdit_Field_ProcessName"), "proc", focusChanged.ProcessName);
                AddField(_loc.GetString("DialogEdit_Field_Hwnd"), "hwnd", focusChanged.Hwnd?.ToString() ?? "");
                AddField(_loc.GetString("DialogEdit_Field_Pid"), "pid", focusChanged.ProcessId?.ToString() ?? "");
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
            _ => string.Empty
        };
    }

    private ushort UShort(string fieldKey) => ushort.Parse(Str(fieldKey));

    private int Int(string fieldKey) => int.Parse(Str(fieldKey));

    private double Double(string fieldKey) =>
        double.Parse(Str(fieldKey), CultureInfo.InvariantCulture);

    private bool Bool(string fieldKey) => bool.Parse(Str(fieldKey));

    private ulong? ULongOrNull(string fieldKey)
    {
        var trimmedText = Str(fieldKey);
        return string.IsNullOrEmpty(trimmedText) ? null : ulong.Parse(trimmedText);
    }

    private uint? UIntOrNull(string fieldKey)
    {
        var trimmedText = Str(fieldKey);
        return string.IsNullOrEmpty(trimmedText) ? null : uint.Parse(trimmedText);
    }
}
