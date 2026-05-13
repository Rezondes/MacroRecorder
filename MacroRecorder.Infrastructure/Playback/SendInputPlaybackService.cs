using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.Infrastructure.Playback;

public sealed class SendInputPlaybackService : IPlaybackService
{
    private static readonly int InputStructSize = Marshal.SizeOf<NativeMethods.INPUT>();

    /// <summary>Min interval between playback countdown UI updates (wall clock). Long waits call into
    /// <see cref="MaybeUpdatePlayingRemaining"/> every loop chunk; throttle keeps dispatcher load low and aligns
    /// visible seconds to about once per second.</summary>
    private const int PlayingUiThrottleMs = 1000;

    private readonly object _playLock = new();
    private readonly NativeMethods.LowLevelProc _interruptKbProc;
    private readonly NativeMethods.LowLevelProc _interruptMsProc;
    private readonly Func<IPlaybackUiFeedback?> _resolveFeedback;
    private CancellationTokenSource? _interruptCts;
    private nint _interruptKeyboardHook;
    private nint _interruptMouseHook;
    private Stopwatch? _userInterruptGraceStopwatch;
    private int _userInterruptGraceMs;

    public SendInputPlaybackService(Func<IPlaybackUiFeedback?> resolveFeedback)
    {
        _resolveFeedback = resolveFeedback;
        _interruptKbProc = InterruptKeyboardLowLevelHook;
        _interruptMsProc = InterruptMouseLowLevelHook;
    }

    public Task PlayAsync(Macro macro, CancellationToken cancellationToken = default, int userInputInterruptGraceMilliseconds = 0) =>
        Task.Run(() =>
        {
            lock (_playLock)
                RunPlayLocked(macro, cancellationToken, userInputInterruptGraceMilliseconds);
        }, cancellationToken);

    public void RequestUserCancel() => _interruptCts?.Cancel();

    private void RunPlayLocked(Macro macro, CancellationToken cancellationToken, int userInputInterruptGraceMilliseconds)
    {
        if (macro.Events.Count == 0)
            return;

        using var userInterrupt = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, userInterrupt.Token);
        _interruptCts = userInterrupt;
        var module = NativeMethods.GetModuleHandle(null);
        _interruptKeyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _interruptKbProc, module, 0);
        _interruptMouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _interruptMsProc, module, 0);

        if (_interruptKeyboardHook == nint.Zero || _interruptMouseHook == nint.Zero)
        {
            CleanupInterruptHooks();
            _interruptCts = null;
            throw new InvalidOperationException(
                "Playback: failed to install low-level keyboard or mouse hooks for user-interrupt detection.");
        }

        _userInterruptGraceMs = Math.Clamp(userInputInterruptGraceMilliseconds, 0, 300_000);
        _userInterruptGraceStopwatch = Stopwatch.StartNew();

        var ordered = macro.Events.OrderBy(recordedEvent => recordedEvent.Sequence).ToList();
        var estimatedPlay = PlaybackDurationEstimator.EstimateTotalPlaybackDuration(ordered);
        var overlayBegan = false;
        var feedback = _resolveFeedback();

        try
        {
            feedback?.Begin(macro, _userInterruptGraceMs, estimatedPlay);
            overlayBegan = true;

            if (_userInterruptGraceMs > 0)
            {
                var graceSw = Stopwatch.StartNew();
                while (graceSw.ElapsedMilliseconds < _userInterruptGraceMs)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var remMs = _userInterruptGraceMs - graceSw.ElapsedMilliseconds;
                    if (remMs < 0)
                        remMs = 0;
                    feedback?.UpdateStartDelayRemaining(TimeSpan.FromMilliseconds(remMs));
                    var chunk = (int)Math.Min(100, remMs);
                    if (chunk <= 0)
                        break;
                    Task.Delay(chunk, linked.Token).GetAwaiter().GetResult();
                }

                feedback?.UpdateStartDelayRemaining(TimeSpan.Zero);
            }
            else
                feedback?.UpdatePlayingRemaining(estimatedPlay);

            PlayCore(ordered, estimatedPlay, linked.Token, feedback);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            throw new PlaybackInterruptedByUserException();
        }
        finally
        {
            if (overlayBegan)
                feedback?.End();

            CleanupInterruptHooks();
            _interruptCts = null;
            _userInterruptGraceStopwatch = null;
            _userInterruptGraceMs = 0;
        }
    }

    private void CleanupInterruptHooks()
    {
        if (_interruptKeyboardHook != nint.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_interruptKeyboardHook);
            _interruptKeyboardHook = nint.Zero;
        }

        if (_interruptMouseHook != nint.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_interruptMouseHook);
            _interruptMouseHook = nint.Zero;
        }
    }

    private nint InterruptKeyboardLowLevelHook(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && _interruptCts is { IsCancellationRequested: false })
        {
            var keyboardLowLevel = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if ((keyboardLowLevel.flags & NativeMethods.LLKHF_INJECTED) == 0
                && (_userInterruptGraceMs == 0
                    || (_userInterruptGraceStopwatch?.ElapsedMilliseconds ?? 0) >= _userInterruptGraceMs))
                _interruptCts.Cancel();
        }

        return NativeMethods.CallNextHookEx(_interruptKeyboardHook, nCode, wParam, lParam);
    }

    private nint InterruptMouseLowLevelHook(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && _interruptCts is { IsCancellationRequested: false })
        {
            var mouseLowLevel = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            if ((mouseLowLevel.flags & NativeMethods.LLMHF_INJECTED) == 0
                && (_userInterruptGraceMs == 0
                    || (_userInterruptGraceStopwatch?.ElapsedMilliseconds ?? 0) >= _userInterruptGraceMs))
                _interruptCts.Cancel();
        }

        return NativeMethods.CallNextHookEx(_interruptMouseHook, nCode, wParam, lParam);
    }

    private static void PumpWindowsMessages()
    {
        while (NativeMethods.PeekMessage(out var windowsMessage, nint.Zero, 0, 0, NativeMethods.PM_REMOVE))
        {
            if (windowsMessage.message == NativeMethods.WM_QUIT)
                break;
            _ = NativeMethods.TranslateMessage(ref windowsMessage);
            _ = NativeMethods.DispatchMessage(ref windowsMessage);
        }
    }

    private void PlayCore(
        IReadOnlyList<RecordedInputEvent> ordered,
        TimeSpan totalEstimated,
        CancellationToken cancellationToken,
        IPlaybackUiFeedback? feedback)
    {
        var sw = Stopwatch.StartNew();
        feedback?.UpdatePlayingRemaining(ClampRemaining(totalEstimated, sw.Elapsed));
        var lastUiMs = Environment.TickCount64;

        var playbackCursor = TimeSpan.Zero;
        foreach (var recordedEvent in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            playbackCursor += recordedEvent.DelayBefore;
            WaitUntil(sw, playbackCursor, cancellationToken, totalEstimated, ref lastUiMs, feedback);
            MaybeUpdatePlayingRemaining(totalEstimated, sw, ref lastUiMs, feedback);

            switch (recordedEvent)
            {
                case KeyDownRecordedEvent keyDown:
                    SendKey(keyDown.ScanCode, keyDown.IsExtendedKey, false);
                    break;
                case KeyUpRecordedEvent keyUp:
                    SendKey(keyUp.ScanCode, keyUp.IsExtendedKey, true);
                    break;
                case MouseMoveRecordedEvent mouseMove:
                    SendMouseMoveAbsolute(mouseMove.ScreenX, mouseMove.ScreenY);
                    break;
                case MouseButtonDownRecordedEvent mouseButtonDown:
                    SendMouseButton(mouseButtonDown.Button, true, mouseButtonDown.ScreenX, mouseButtonDown.ScreenY);
                    break;
                case MouseButtonUpRecordedEvent mouseButtonUp:
                    SendMouseButton(mouseButtonUp.Button, false, mouseButtonUp.ScreenX, mouseButtonUp.ScreenY);
                    break;
                case MouseWheelRecordedEvent mouseWheel:
                    SendMouseMoveAbsolute(mouseWheel.ScreenX, mouseWheel.ScreenY);
                    SendWheel(mouseWheel.WheelDelta, mouseWheel.IsHorizontal);
                    break;
                case FocusChangedRecordedEvent focusChanged:
                    TryFocus(focusChanged);
                    break;
                case SyntheticWaitRecordedEvent syntheticWait:
                    SleepCancellable(
                        syntheticWait.AdditionalDelay,
                        cancellationToken,
                        totalEstimated,
                        sw,
                        ref lastUiMs,
                        feedback);
                    playbackCursor += syntheticWait.AdditionalDelay;
                    break;
            }

            MaybeUpdatePlayingRemaining(totalEstimated, sw, ref lastUiMs, feedback);
        }

        feedback?.UpdatePlayingRemaining(TimeSpan.Zero);
    }

    private void MaybeUpdatePlayingRemaining(
        TimeSpan totalEstimated,
        Stopwatch sessionSw,
        ref long lastUiMs,
        IPlaybackUiFeedback? feedback)
    {
        var now = Environment.TickCount64;
        if (now - lastUiMs < PlayingUiThrottleMs)
            return;

        lastUiMs = now;
        feedback?.UpdatePlayingRemaining(ClampRemaining(totalEstimated, sessionSw.Elapsed));
    }

    private static TimeSpan ClampRemaining(TimeSpan totalEstimated, TimeSpan elapsed)
    {
        var rem = totalEstimated - elapsed;
        return rem < TimeSpan.Zero ? TimeSpan.Zero : rem;
    }

    private void WaitUntil(
        Stopwatch sw,
        TimeSpan target,
        CancellationToken cancellationToken,
        TimeSpan totalEstimated,
        ref long lastUiMs,
        IPlaybackUiFeedback? feedback)
    {
        // Avoid tiny sleeps in a tight loop: default system timer resolution (~15.6 ms) inflates short waits.
        // Use Task.Delay (with cancellation) so each chunk cooperates with CancellationToken; work runs on a
        // thread-pool thread inside Task.Run(PlayAsync), not the WPF UI thread.
        const int maxSleepChunkMs = 50;
        while (sw.Elapsed < target)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PumpWindowsMessages();
            MaybeUpdatePlayingRemaining(totalEstimated, sw, ref lastUiMs, feedback);
            var remaining = target - sw.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            if (remaining > TimeSpan.FromMilliseconds(2))
            {
                var sleepMs = (int)Math.Min(
                    Math.Max(1, remaining.TotalMilliseconds - 0.5),
                    maxSleepChunkMs);
                Task.Delay(TimeSpan.FromMilliseconds(sleepMs), cancellationToken).GetAwaiter().GetResult();
            }
            else
            {
                PumpWindowsMessages();
                Thread.SpinWait(100);
            }

            MaybeUpdatePlayingRemaining(totalEstimated, sw, ref lastUiMs, feedback);
        }
    }

    private void SleepCancellable(
        TimeSpan delay,
        CancellationToken cancellationToken,
        TimeSpan totalEstimated,
        Stopwatch sessionSw,
        ref long lastUiMs,
        IPlaybackUiFeedback? feedback)
    {
        if (delay <= TimeSpan.Zero)
            return;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < delay)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PumpWindowsMessages();
            MaybeUpdatePlayingRemaining(totalEstimated, sessionSw, ref lastUiMs, feedback);
            var left = delay - sw.Elapsed;
            var sleepChunkMilliseconds = (int)Math.Min(15, Math.Ceiling(left.TotalMilliseconds));
            if (sleepChunkMilliseconds < 1)
            {
                PumpWindowsMessages();
                Thread.SpinWait(50);
            }
            else
            {
                Task.Delay(TimeSpan.FromMilliseconds(sleepChunkMilliseconds), cancellationToken)
                    .GetAwaiter()
                    .GetResult();
            }

            MaybeUpdatePlayingRemaining(totalEstimated, sessionSw, ref lastUiMs, feedback);
        }
    }

    private static void SendKey(ushort scanCode, bool extended, bool keyUp)
    {
        uint flags = NativeMethods.KEYEVENTF_SCANCODE;
        if (keyUp)
            flags |= NativeMethods.KEYEVENTF_KEYUP;
        if (extended)
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        _ = NativeMethods.SendInput(1, new[] { input }, InputStructSize);
    }

    private static void SendMouseMoveAbsolute(int x, int y)
    {
        var (nx, ny) = NormalizeAbsolute(x, y);
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = nx,
                    dy = ny,
                    mouseData = 0,
                    dwFlags = NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_ABSOLUTE |
                              NativeMethods.MOUSEEVENTF_VIRTUALDESK,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        _ = NativeMethods.SendInput(1, new[] { input }, InputStructSize);
    }

    private static (int nx, int ny) NormalizeAbsolute(int x, int y)
    {
        var vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        if (vw <= 1) vw = 1;
        if (vh <= 1) vh = 1;
        var nx = (int)((x - vx) * 65535.0 / (vw - 1));
        var ny = (int)((y - vy) * 65535.0 / (vh - 1));
        return (nx, ny);
    }

    private static void SendMouseButton(MouseButtonKind button, bool down, int x, int y)
    {
        SendMouseMoveAbsolute(x, y);
        uint flag;
        uint data = 0;

        switch (button)
        {
            case MouseButtonKind.Left:
                flag = down ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP;
                break;
            case MouseButtonKind.Right:
                flag = down ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP;
                break;
            case MouseButtonKind.Middle:
                flag = down ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP;
                break;
            case MouseButtonKind.X1:
            case MouseButtonKind.X2:
                flag = down ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP;
                var which = button == MouseButtonKind.X1 ? NativeMethods.XBUTTON1 : NativeMethods.XBUTTON2;
                data = which << 16;
                break;
            default:
                return;
        }

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = data,
                    dwFlags = flag,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        _ = NativeMethods.SendInput(1, new[] { input }, InputStructSize);
    }

    private static void SendWheel(int delta, bool horizontal)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = unchecked((uint)delta),
                    dwFlags = horizontal ? NativeMethods.MOUSEEVENTF_HWHEEL : NativeMethods.MOUSEEVENTF_WHEEL,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        _ = NativeMethods.SendInput(1, new[] { input }, InputStructSize);
    }

    private static void TryFocus(FocusChangedRecordedEvent f)
    {
        if (f.Hwnd is null)
            return;
        var hwnd = (nint)f.Hwnd.Value;
        if (!NativeMethods.IsWindow(hwnd))
            return;
        _ = NativeMethods.SetForegroundWindow(hwnd);
    }
}
