using System.Runtime.InteropServices;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.Infrastructure.Input;

/// <summary>
/// Swallows physical left/right Windows keys via <c>WH_KEYBOARD_LL</c> while active (so Start does not open), only
/// when the foreground window belongs to the same root HWND. On each physical key-down, invokes
/// <paramref name="onWinKeyDownPhysical"/> so the UI can still record the key.
/// </summary>
public sealed class WinKeyLowLevelHookScope : IDisposable
{
    private const uint VkLeftWindows = 0x5B;
    private const uint VkRightWindows = 0x5C;

    private readonly nint _rootHwnd;
    private readonly Action<uint, ushort, bool> _onWinKeyDownPhysical;
    private readonly NativeMethods.LowLevelProc _proc;
    private nint _hook;
    private bool _disposed;

    public WinKeyLowLevelHookScope(nint rootWindowHandle, Action<uint, ushort, bool> onWinKeyDownPhysical)
    {
        _rootHwnd = rootWindowHandle;
        _onWinKeyDownPhysical = onWinKeyDownPhysical;
        _proc = HookProc;
        var module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, module, 0);
    }

    private bool IsOurWindowInForeground()
    {
        if (_rootHwnd == nint.Zero)
            return false;
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == nint.Zero)
            return false;
        if (fg == _rootHwnd)
            return true;
        var root = NativeMethods.GetAncestor(fg, NativeMethods.GA_ROOT);
        return root == _rootHwnd;
    }

    private nint HookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0 || _hook == nint.Zero)
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

        var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        var isWinKey = kb.vkCode == VkLeftWindows || kb.vkCode == VkRightWindows;
        if (!isWinKey || !IsOurWindowInForeground())
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

        if ((kb.flags & NativeMethods.LLKHF_INJECTED) != 0)
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

        var msg = (uint)(nint)wParam;
        if (msg == (uint)NativeMethods.WM_KEYDOWN || msg == (uint)NativeMethods.WM_SYSKEYDOWN)
        {
            var scan = (ushort)kb.scanCode;
            var extended = (kb.flags & NativeMethods.LLKHF_EXTENDED) != 0;
            _onWinKeyDownPhysical(kb.vkCode, scan, extended);
        }

        return (nint)1;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_hook != nint.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
