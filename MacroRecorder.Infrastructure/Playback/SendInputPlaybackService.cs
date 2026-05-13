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

    /// <summary>Throttle start-delay overlay updates; avoids flooding the UI dispatcher while the grace loop runs.</summary>
    private const int StartDelayUiThrottleMs = 250;

    /// <summary>After <see cref="NativeMethods.SetForegroundWindow"/> or restore from minimized, allow the shell to settle before SendInput.</summary>
    private const int PlaybackFocusStabilizationDelayMs = 250;

    private readonly object _playLock = new();
    private readonly NativeMethods.LowLevelProc _interruptKbProc;
    private readonly NativeMethods.LowLevelProc _interruptMsProc;
    private readonly Func<IPlaybackUiFeedback?> _resolveFeedback;
    private CancellationTokenSource? _interruptCts;
    private volatile bool _explicitPlaybackAbortFromHost;
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

    public Task PlayAsync(
        Macro macro,
        CancellationToken cancellationToken = default,
        int userInputInterruptGraceMilliseconds = 0,
        bool playbackFocusBringWindowToForeground = true,
        bool playbackFocusRestoreIfMinimized = true) =>
        Task.Run(() =>
        {
            lock (_playLock)
                RunPlayLocked(
                    macro,
                    cancellationToken,
                    userInputInterruptGraceMilliseconds,
                    playbackFocusBringWindowToForeground,
                    playbackFocusRestoreIfMinimized);
        }, cancellationToken);

    public void RequestUserCancel()
    {
        if (_interruptCts is { IsCancellationRequested: false })
        {
            _explicitPlaybackAbortFromHost = true;
            _interruptCts.Cancel();
        }
    }

    private sealed class FocusBoundPlaybackState
    {
        public nint CurrentHwnd;
    }

    private void RunPlayLocked(
        Macro macro,
        CancellationToken cancellationToken,
        int userInputInterruptGraceMilliseconds,
        bool playbackFocusBringWindowToForeground,
        bool playbackFocusRestoreIfMinimized)
    {
        if (macro.Events.Count == 0)
            return;

        using var userInterrupt = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, userInterrupt.Token);
        _interruptCts = userInterrupt;

        _userInterruptGraceMs = Math.Clamp(userInputInterruptGraceMilliseconds, 0, 300_000);
        _userInterruptGraceStopwatch = Stopwatch.StartNew();

        var module = NativeMethods.GetModuleHandle(null);
        var ordered = macro.Events.OrderBy(recordedEvent => recordedEvent.Sequence).ToList();
        var estimatedPlay = PlaybackDurationEstimator.EstimateTotalPlaybackDuration(ordered);
        var overlayBegan = false;
        var feedback = _resolveFeedback();

        FocusBoundPlaybackState? focusBoundState = null;
        if (macro.Metadata.UseFocusBoundMouseCoordinates)
        {
            FocusWindowMatcher.ValidateFocusBoundTimeline(macro, ordered);
            focusBoundState = new FocusBoundPlaybackState
            {
                CurrentHwnd = FocusWindowMatcher.ResolveInitialFocusBoundHwnd(macro, ordered)
            };
        }

        try
        {
            feedback?.Begin(macro, _userInterruptGraceMs, estimatedPlay);
            overlayBegan = true;

            // Low-level hooks are installed only after the start delay. WH_MOUSE_LL / WH_KEYBOARD_LL run on this
            // thread and would otherwise delay system input during grace; user cancel during grace uses CTS only.
            if (_userInterruptGraceMs > 0)
            {
                var graceSw = Stopwatch.StartNew();
                var lastStartDelayUiMs = Environment.TickCount64 - StartDelayUiThrottleMs;
                while (graceSw.ElapsedMilliseconds < _userInterruptGraceMs)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    var remMs = _userInterruptGraceMs - graceSw.ElapsedMilliseconds;
                    if (remMs < 0)
                        remMs = 0;
                    var nowUi = Environment.TickCount64;
                    if (nowUi - lastStartDelayUiMs >= StartDelayUiThrottleMs)
                    {
                        lastStartDelayUiMs = nowUi;
                        feedback?.UpdateStartDelayRemaining(TimeSpan.FromMilliseconds(remMs));
                    }

                    var chunk = (int)Math.Min(100, remMs);
                    if (chunk <= 0)
                        break;
                    Task.Delay(chunk, linked.Token).GetAwaiter().GetResult();
                }

                feedback?.UpdateStartDelayRemaining(TimeSpan.Zero);
            }
            else
                feedback?.UpdatePlayingRemaining(estimatedPlay);

            TryInstallInterruptHooks(module);

            PlayCore(
                ordered,
                estimatedPlay,
                linked.Token,
                feedback,
                focusBoundState,
                playbackFocusBringWindowToForeground,
                playbackFocusRestoreIfMinimized);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            if (_explicitPlaybackAbortFromHost)
                throw new PlaybackAbortedByUserRequestException();

            throw new PlaybackInterruptedByUserException();
        }
        finally
        {
            _explicitPlaybackAbortFromHost = false;
            if (overlayBegan)
                feedback?.End();

            CleanupInterruptHooks();
            _interruptCts = null;
            _userInterruptGraceStopwatch = null;
            _userInterruptGraceMs = 0;
        }
    }

    private void TryInstallInterruptHooks(nint module)
    {
        if (_interruptKeyboardHook != nint.Zero || _interruptMouseHook != nint.Zero)
            return;

        _interruptKeyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _interruptKbProc, module, 0);
        _interruptMouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _interruptMsProc, module, 0);

        if (_interruptKeyboardHook != nint.Zero && _interruptMouseHook != nint.Zero)
            return;

        CleanupInterruptHooks();
        throw new InvalidOperationException(
            "Playback: failed to install low-level keyboard or mouse hooks for user-interrupt detection.");
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

    private static (int screenX, int screenY) ClientCoordsToScreen(nint hwnd, int clientX, int clientY)
    {
        var p = new NativeMethods.POINT { X = clientX, Y = clientY };
        _ = NativeMethods.ClientToScreen(hwnd, ref p);
        return (p.X, p.Y);
    }

    private void PlayCore(
        IReadOnlyList<RecordedInputEvent> ordered,
        TimeSpan totalEstimated,
        CancellationToken cancellationToken,
        IPlaybackUiFeedback? feedback,
        FocusBoundPlaybackState? focusBoundState,
        bool playbackFocusBringWindowToForeground,
        bool playbackFocusRestoreIfMinimized)
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

            var focusHwnd = focusBoundState?.CurrentHwnd ?? nint.Zero;

            switch (recordedEvent)
            {
                case KeyDownRecordedEvent keyDown:
                    SendKey(keyDown.ScanCode, keyDown.IsExtendedKey, false);
                    break;
                case KeyUpRecordedEvent keyUp:
                    SendKey(keyUp.ScanCode, keyUp.IsExtendedKey, true);
                    break;
                case MouseMoveRecordedEvent mouseMove:
                {
                    var (sx, sy) = focusHwnd != nint.Zero
                        ? ClientCoordsToScreen(focusHwnd, mouseMove.ScreenX, mouseMove.ScreenY)
                        : (mouseMove.ScreenX, mouseMove.ScreenY);
                    SendMouseMoveAbsolute(sx, sy);
                    break;
                }
                case MouseButtonDownRecordedEvent mouseButtonDown:
                {
                    var (sx, sy) = focusHwnd != nint.Zero
                        ? ClientCoordsToScreen(focusHwnd, mouseButtonDown.ScreenX, mouseButtonDown.ScreenY)
                        : (mouseButtonDown.ScreenX, mouseButtonDown.ScreenY);
                    SendMouseButton(mouseButtonDown.Button, true, sx, sy);
                    break;
                }
                case MouseButtonUpRecordedEvent mouseButtonUp:
                {
                    var (sx, sy) = focusHwnd != nint.Zero
                        ? ClientCoordsToScreen(focusHwnd, mouseButtonUp.ScreenX, mouseButtonUp.ScreenY)
                        : (mouseButtonUp.ScreenX, mouseButtonUp.ScreenY);
                    SendMouseButton(mouseButtonUp.Button, false, sx, sy);
                    break;
                }
                case MouseWheelRecordedEvent mouseWheel:
                {
                    var (sx, sy) = focusHwnd != nint.Zero
                        ? ClientCoordsToScreen(focusHwnd, mouseWheel.ScreenX, mouseWheel.ScreenY)
                        : (mouseWheel.ScreenX, mouseWheel.ScreenY);
                    SendMouseMoveAbsolute(sx, sy);
                    SendWheel(mouseWheel.WheelDelta, mouseWheel.IsHorizontal);
                    break;
                }
                case FocusChangedRecordedEvent focusChanged:
                {
                    nint? resolvedHwnd = null;
                    if (focusBoundState is not null && focusChanged.Hwnd is not null)
                        resolvedHwnd = FocusWindowMatcher.ResolveForPlayback(focusChanged);

                    if (focusChanged.Hwnd is not null)
                    {
                        var hwndForFocus = resolvedHwnd ?? (nint)focusChanged.Hwnd.Value;
                        var needFocusStabilizationDelay = ApplyPlaybackFocusToWindow(
                            hwndForFocus,
                            playbackFocusBringWindowToForeground,
                            playbackFocusRestoreIfMinimized);
                        if (needFocusStabilizationDelay)
                        {
                            var stabilization = TimeSpan.FromMilliseconds(PlaybackFocusStabilizationDelayMs);
                            SleepCancellable(
                                stabilization,
                                cancellationToken,
                                totalEstimated,
                                sw,
                                ref lastUiMs,
                                feedback);
                            playbackCursor += stabilization;
                        }
                    }

                    if (focusBoundState is not null)
                    {
                        focusBoundState.CurrentHwnd = focusChanged.Hwnd is null
                            ? nint.Zero
                            : resolvedHwnd!.Value;
                    }

                    break;
                }
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

    /// <returns>True if the window was restored from minimized and/or brought to the foreground; caller may wait so the shell can settle.</returns>
    private static bool ApplyPlaybackFocusToWindow(nint hwnd, bool bringToForeground, bool restoreIfMinimized)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
            return false;

        var needStabilizationDelay = false;
        if (restoreIfMinimized && NativeMethods.IsIconic(hwnd))
        {
            _ = NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            needStabilizationDelay = true;
        }

        if (bringToForeground)
        {
            if (NativeMethods.GetForegroundWindow() != hwnd)
                needStabilizationDelay = true;
            _ = NativeMethods.SetForegroundWindow(hwnd);
        }

        return needStabilizationDelay;
    }
}
