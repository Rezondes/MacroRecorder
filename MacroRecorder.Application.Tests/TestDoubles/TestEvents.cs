using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.TestDoubles;

internal static class TestEvents
{
    public static KeyDownRecordedEvent KeyDown(
        ulong sequence = 1,
        TimeSpan? delayBefore = null,
        ushort virtualKey = 0x41) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore ?? TimeSpan.Zero,
            Vk = virtualKey,
            ScanCode = 0,
            IsExtendedKey = false,
            IsAltDown = false,
            IsInjected = false
        };

    public static SyntheticWaitRecordedEvent SyntheticWait(
        ulong sequence,
        TimeSpan delayBefore,
        TimeSpan additionalDelay) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore,
            AdditionalDelay = additionalDelay
        };

    public static MouseMoveRecordedEvent MouseMove(ulong sequence, int screenX, int screenY, TimeSpan delayBefore) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore,
            ScreenX = screenX,
            ScreenY = screenY
        };

    public static MouseButtonDownRecordedEvent MouseLeftDown(ulong sequence, int screenX, int screenY, TimeSpan delayBefore) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore,
            Button = MouseButtonKind.Left,
            ScreenX = screenX,
            ScreenY = screenY
        };

    public static MouseButtonUpRecordedEvent MouseLeftUp(ulong sequence, int screenX, int screenY, TimeSpan delayBefore) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore,
            Button = MouseButtonKind.Left,
            ScreenX = screenX,
            ScreenY = screenY
        };

    public static FocusChangedRecordedEvent FocusLost(ulong sequence, TimeSpan delayBefore) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore,
            Hwnd = null,
            WindowTitle = "",
            ProcessName = ""
        };

    public static FocusChangedRecordedEvent HostFocus(
        ulong sequence,
        TimeSpan delayBefore,
        string hostProcessName) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore,
            Hwnd = 1,
            WindowTitle = "",
            ProcessName = hostProcessName
        };

    public static KeyUpRecordedEvent KeyUp(ulong sequence = 1, TimeSpan? delayBefore = null) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore ?? TimeSpan.Zero,
            Vk = 0x41,
            ScanCode = 0,
            IsExtendedKey = false,
            IsAltDown = false,
            IsInjected = false
        };

    public static MouseWheelRecordedEvent MouseWheel(ulong sequence, int wheelDelta, TimeSpan delayBefore) =>
        new()
        {
            Sequence = sequence,
            DelayBefore = delayBefore,
            ScreenX = 0,
            ScreenY = 0,
            WheelDelta = wheelDelta,
            IsHorizontal = false
        };
}
