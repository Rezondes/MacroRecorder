using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Views.Editor;

public partial class EditSingleEventDialog : Window
{
    private readonly RecordedInputEvent _original;
    private readonly IUiLocalizer _loc;
    private readonly Dictionary<string, TextBox> _fields = new();

    public EditSingleEventDialog(RecordedInputEvent original, IUiLocalizer loc)
    {
        _original = original;
        _loc = loc;
        InitializeComponent();
        BuildFields();
    }

    public RecordedInputEvent? ResultEvent { get; private set; }

    private void AddField(string label, string fieldKey, string value)
    {
        FieldsPanel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
            Margin = new Thickness(0, 10, 0, 4)
        });
        var textBox = new TextBox
        {
            Text = value,
            Tag = fieldKey,
            FontSize = 13,
            MinHeight = 32,
            Padding = new Thickness(8, 4, 8, 4)
        };
        FieldsPanel.Children.Add(textBox);
        _fields[fieldKey] = textBox;
    }

    private void BuildFields()
    {
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
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseMove.ScreenX.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseMove.ScreenY.ToString());
                break;
            case MouseButtonDownRecordedEvent mouseButtonDown:
                AddField(_loc.GetString("DialogEdit_Field_Button"), "btn", mouseButtonDown.Button.ToString());
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseButtonDown.ScreenX.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseButtonDown.ScreenY.ToString());
                break;
            case MouseButtonUpRecordedEvent mouseButtonUp:
                AddField(_loc.GetString("DialogEdit_Field_Button"), "btn", mouseButtonUp.Button.ToString());
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseButtonUp.ScreenX.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseButtonUp.ScreenY.ToString());
                break;
            case MouseWheelRecordedEvent mouseWheel:
                AddField(_loc.GetString("DialogEdit_Field_X"), "x", mouseWheel.ScreenX.ToString());
                AddField(_loc.GetString("DialogEdit_Field_Y"), "y", mouseWheel.ScreenY.ToString());
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
                FieldsPanel.Children.Add(new TextBlock
                {
                    Text = _loc.GetString("DialogEdit_EventNotEditable"),
                    TextWrapping = TextWrapping.Wrap
                });
                break;
        }
    }

    private void OnOk(object sender, RoutedEventArgs routedEventArgs)
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
                    Button = Enum.Parse<MouseButtonKind>(Str("btn"), ignoreCase: true),
                    ScreenX = Int("x"),
                    ScreenY = Int("y")
                },
                MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with
                {
                    Button = Enum.Parse<MouseButtonKind>(Str("btn"), ignoreCase: true),
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
            MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ResultEvent is null)
            return;

        DialogResult = true;
        Close();
    }

    private string Str(string fieldKey) => _fields[fieldKey].Text.Trim();

    private ushort UShort(string fieldKey) => ushort.Parse(Str(fieldKey));

    private int Int(string fieldKey) => int.Parse(Str(fieldKey));

    private double Double(string fieldKey) =>
        double.Parse(Str(fieldKey), System.Globalization.CultureInfo.InvariantCulture);

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
