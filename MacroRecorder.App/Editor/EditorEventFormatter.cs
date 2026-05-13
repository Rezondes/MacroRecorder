using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Input;

namespace MacroRecorder.App.Editor;

public static class EditorEventFormatter
{
    /// <summary>Segoe MDL2 Assets codepoint (single char) for the editor timeline icon column.</summary>
    public static string TimelineIconGlyph(RecordedInputEvent recordedEvent) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent or KeyUpRecordedEvent => "\uE92E",
            MouseMoveRecordedEvent => "\uE7C9",
            MouseButtonDownRecordedEvent or MouseButtonUpRecordedEvent => "\uE962",
            MouseWheelRecordedEvent => "\uEE94",
            FocusChangedRecordedEvent => "\uE8F9",
            SyntheticWaitRecordedEvent => "\uE916",
            _ => "\uE9CE"
        };

    public static string ActionLabel(RecordedInputEvent recordedEvent, IUiLocalizer loc) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent => loc.GetString("Event_Action_KeyDown"),
            KeyUpRecordedEvent => loc.GetString("Event_Action_KeyUp"),
            MouseMoveRecordedEvent => loc.GetString("Event_Action_MouseMove"),
            MouseButtonDownRecordedEvent mouseButtonDown =>
                loc.GetString("Event_Action_MouseButtonDownFormat", ButtonLabel(mouseButtonDown.Button, loc)),
            MouseButtonUpRecordedEvent mouseButtonUp =>
                loc.GetString("Event_Action_MouseButtonUpFormat", ButtonLabel(mouseButtonUp.Button, loc)),
            MouseWheelRecordedEvent => loc.GetString("Event_Action_MouseWheel"),
            FocusChangedRecordedEvent { Hwnd: null } => loc.GetString("Event_Action_FocusLost"),
            FocusChangedRecordedEvent => loc.GetString("Event_Action_FocusChanged"),
            SyntheticWaitRecordedEvent => loc.GetString("Event_Action_SyntheticWait"),
            _ => recordedEvent.GetType().Name
        };

    public static string ValueText(RecordedInputEvent recordedEvent, IUiLocalizer loc) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => KeyDisplayName.GetName(keyDown.Vk, keyDown.ScanCode, keyDown.IsExtendedKey),
            KeyUpRecordedEvent keyUp =>
                KeyDisplayName.GetName(keyUp.Vk, keyUp.ScanCode, keyUp.IsExtendedKey),
            MouseMoveRecordedEvent mouseMove =>
                loc.GetString(
                    "Editor_Value_MouseMoveFormat",
                    mouseMove.ScreenX,
                    mouseMove.ScreenY,
                    FormatElapsed(mouseMove.DelayBefore, loc)),
            MouseButtonDownRecordedEvent mouseButtonDown =>
                $"{mouseButtonDown.ScreenX}, {mouseButtonDown.ScreenY}",
            MouseButtonUpRecordedEvent mouseButtonUp =>
                $"{mouseButtonUp.ScreenX}, {mouseButtonUp.ScreenY}",
            MouseWheelRecordedEvent mouseWheel =>
                loc.GetString(
                    "Editor_Value_MouseWheelFormat",
                    mouseWheel.ScreenX,
                    mouseWheel.ScreenY,
                    mouseWheel.WheelDelta,
                    mouseWheel.IsHorizontal ? loc.GetString("Editor_Value_MouseWheelHorizontalSuffix") : ""),
            FocusChangedRecordedEvent { Hwnd: null } => loc.GetString("Editor_Value_FocusLost"),
            FocusChangedRecordedEvent focusChanged =>
                loc.GetString(
                    "Editor_Value_FocusFormat",
                    focusChanged.ProcessName,
                    focusChanged.WindowTitle,
                    focusChanged.Hwnd is { } windowHandle
                        ? loc.GetString("Editor_Value_FocusHwndFormat", windowHandle)
                        : ""),
            SyntheticWaitRecordedEvent syntheticWait =>
                $"{syntheticWait.AdditionalDelay.TotalMilliseconds:0} ms",
            _ => recordedEvent.ToString() ?? ""
        };

    private static string FormatElapsed(TimeSpan elapsed, IUiLocalizer loc) =>
        elapsed.ToString(@"hh\:mm\:ss\.fff", loc.CurrentUiCulture);

    private static string ButtonLabel(MouseButtonKind mouseButtonKind, IUiLocalizer loc) =>
        mouseButtonKind switch
        {
            MouseButtonKind.Left => loc.GetString("Event_MouseButton_Left"),
            MouseButtonKind.Right => loc.GetString("Event_MouseButton_Right"),
            MouseButtonKind.Middle => loc.GetString("Event_MouseButton_Middle"),
            MouseButtonKind.X1 => loc.GetString("Event_MouseButton_X1"),
            MouseButtonKind.X2 => loc.GetString("Event_MouseButton_X2"),
            _ => mouseButtonKind.ToString()
        };
}
