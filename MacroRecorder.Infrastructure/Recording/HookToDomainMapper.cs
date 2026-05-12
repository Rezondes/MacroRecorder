using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.Infrastructure.Recording;

internal static class HookToDomainMapper
{
    public static RecordedInputEvent? FromKeyboard(int windowMessage, NativeMethods.KBDLLHOOKSTRUCT keyboardLowLevel)
    {
        var virtualKey = (ushort)keyboardLowLevel.vkCode;
        var scanCode = (ushort)keyboardLowLevel.scanCode;
        var extended = (keyboardLowLevel.flags & 0x01) != 0;
        var injected = (keyboardLowLevel.flags & 0x10) != 0;
        var altDown = (keyboardLowLevel.flags & 0x20) != 0;
        var isReleased = (keyboardLowLevel.flags & 0x80) != 0;

        if ((windowMessage == NativeMethods.WM_KEYDOWN || windowMessage == NativeMethods.WM_SYSKEYDOWN) && !isReleased)
            return new KeyDownRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Vk = virtualKey,
                ScanCode = scanCode,
                IsExtendedKey = extended,
                IsAltDown = altDown,
                IsInjected = injected,
                RepeatCount = 1
            };

        if ((windowMessage == NativeMethods.WM_KEYUP || windowMessage == NativeMethods.WM_SYSKEYUP) && isReleased)
            return new KeyUpRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Vk = virtualKey,
                ScanCode = scanCode,
                IsExtendedKey = extended,
                IsAltDown = altDown,
                IsInjected = injected
            };

        return null;
    }

    public static RecordedInputEvent? FromMouse(int windowMessage, NativeMethods.MSLLHOOKSTRUCT mouseLowLevel)
    {
        var screenX = mouseLowLevel.pt.X;
        var screenY = mouseLowLevel.pt.Y;

        return (uint)windowMessage switch
        {
            NativeMethods.WM_MOUSEMOVE => new MouseMoveRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                ScreenX = screenX,
                ScreenY = screenY
            },
            NativeMethods.WM_LBUTTONDOWN => new MouseButtonDownRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.Left,
                ScreenX = screenX,
                ScreenY = screenY
            },
            NativeMethods.WM_LBUTTONUP => new MouseButtonUpRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.Left,
                ScreenX = screenX,
                ScreenY = screenY
            },
            NativeMethods.WM_RBUTTONDOWN => new MouseButtonDownRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.Right,
                ScreenX = screenX,
                ScreenY = screenY
            },
            NativeMethods.WM_RBUTTONUP => new MouseButtonUpRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.Right,
                ScreenX = screenX,
                ScreenY = screenY
            },
            NativeMethods.WM_MBUTTONDOWN => new MouseButtonDownRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.Middle,
                ScreenX = screenX,
                ScreenY = screenY
            },
            NativeMethods.WM_MBUTTONUP => new MouseButtonUpRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.Middle,
                ScreenX = screenX,
                ScreenY = screenY
            },
            NativeMethods.WM_XBUTTONDOWN => BuildXButtonDown(mouseLowLevel, screenX, screenY),
            NativeMethods.WM_XBUTTONUP => BuildXButtonUp(mouseLowLevel, screenX, screenY),
            NativeMethods.WM_MOUSEWHEEL => new MouseWheelRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                ScreenX = screenX,
                ScreenY = screenY,
                WheelDelta = GetWheelDelta(mouseLowLevel.mouseData),
                IsHorizontal = false
            },
            NativeMethods.WM_MOUSEHWHEEL => new MouseWheelRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                ScreenX = screenX,
                ScreenY = screenY,
                WheelDelta = GetWheelDelta(mouseLowLevel.mouseData),
                IsHorizontal = true
            },
            _ => null
        };
    }

    private static int GetWheelDelta(uint mouseData) => unchecked((short)(mouseData >> 16));

    private static RecordedInputEvent? BuildXButtonDown(NativeMethods.MSLLHOOKSTRUCT mouseLowLevel, int screenX, int screenY)
    {
        var xButtonWord = (uint)mouseLowLevel.mouseData >> 16;
        if (xButtonWord == NativeMethods.XBUTTON1)
            return new MouseButtonDownRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.X1,
                ScreenX = screenX,
                ScreenY = screenY
            };
        if (xButtonWord == NativeMethods.XBUTTON2)
            return new MouseButtonDownRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.X2,
                ScreenX = screenX,
                ScreenY = screenY
            };
        return null;
    }

    private static RecordedInputEvent? BuildXButtonUp(NativeMethods.MSLLHOOKSTRUCT mouseLowLevel, int screenX, int screenY)
    {
        var xButtonWord = (uint)mouseLowLevel.mouseData >> 16;
        if (xButtonWord == NativeMethods.XBUTTON1)
            return new MouseButtonUpRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.X1,
                ScreenX = screenX,
                ScreenY = screenY
            };
        if (xButtonWord == NativeMethods.XBUTTON2)
            return new MouseButtonUpRecordedEvent
            {
                ElapsedSinceSessionStart = default,
                Sequence = 0,
                Button = MouseButtonKind.X2,
                ScreenX = screenX,
                ScreenY = screenY
            };
        return null;
    }
}
