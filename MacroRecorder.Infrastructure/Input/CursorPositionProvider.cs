using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.Infrastructure.Input;

public sealed class CursorPositionProvider : ICursorPositionProvider
{
    public (int X, int Y) GetScreenPosition()
    {
        if (!NativeMethods.GetCursorPos(out var cursorPoint))
            return (0, 0);
        return (cursorPoint.X, cursorPoint.Y);
    }
}
