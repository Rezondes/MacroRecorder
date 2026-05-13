using System.Runtime.InteropServices;

namespace MacroRecorder.Infrastructure.Interop;

/// <summary>Public cursor helpers for UI layers outside the Infrastructure assembly.</summary>
public static class CursorInterop
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    public static void SetScreenPosition(int x, int y) => SetCursorPos(x, y);
}
