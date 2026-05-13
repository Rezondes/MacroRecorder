using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.Infrastructure.Recording;

public sealed class LowLevelRecordingEngine : IRecordingEngine
{
    private static readonly TimeSpan MinAnchorGap = TimeSpan.FromMilliseconds(1);

    private readonly object _lock = new();
    private readonly List<RecordedInputEvent> _events = new();
    private readonly NativeMethods.LowLevelProc _keyboardProc;
    private readonly NativeMethods.LowLevelProc _mouseProc;
    private long _sequence;
    private volatile bool _hooksReady;
    private Stopwatch? _stopwatch;
    private nint _keyboardHook;
    private nint _mouseHook;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private CancellationTokenSource? _foregroundCts;
    private Task? _foregroundTask;
    private volatile bool _running;
    private nint _lastForeground = nint.Zero;
    private bool _haveLastRecordedMouseMove;
    private int _lastRecordedMouseMoveX;
    private int _lastRecordedMouseMoveY;
    private Action<RecordedInputEvent>? _onEventRecorded;
    private bool _recordMouseMoves = true;
    private int _mouseMoveMinPixelDelta = 5;
    private bool _haveAnchor;
    private TimeSpan _lastAnchorElapsed;
    private TimeSpan _playbackTimelineEnd;
    private bool _useFocusBoundMouseCoordinates;
    private bool _useClientSpaceForMouse;

    public LowLevelRecordingEngine()
    {
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
    }

    public bool IsRunning => _running;

    public void Start(
        Action<RecordedInputEvent>? onEventRecorded = null,
        bool recordMouseMoves = true,
        int mouseMoveMinPixelDelta = 5,
        bool useFocusBoundMouseCoordinates = false)
    {
        if (_running)
            throw new InvalidOperationException("Recording already running.");

        var clampedMinPixels = Math.Clamp(mouseMoveMinPixelDelta, 1, 10_000);

        lock (_lock)
        {
            _events.Clear();
            Interlocked.Exchange(ref _sequence, 0);
            _haveLastRecordedMouseMove = false;
            _recordMouseMoves = recordMouseMoves;
            _mouseMoveMinPixelDelta = clampedMinPixels;
            _haveAnchor = false;
            _lastAnchorElapsed = TimeSpan.Zero;
            _playbackTimelineEnd = TimeSpan.Zero;
            _useFocusBoundMouseCoordinates = useFocusBoundMouseCoordinates;
            _useClientSpaceForMouse = false;
        }

        _onEventRecorded = onEventRecorded;
        _hooksReady = false;
        _lastForeground = NativeMethods.GetForegroundWindow();
        _running = true;
        _foregroundCts = new CancellationTokenSource();
        var token = _foregroundCts.Token;
        _foregroundTask = Task.Run(() => ForegroundLoop(token), token);

        using var ready = new ManualResetEventSlim(false);
        _hookThread = new Thread(() => HookThreadProc(ready)) { IsBackground = true };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        ready.Wait();
        if (!_hooksReady)
        {
            _running = false;
            _onEventRecorded = null;
            _foregroundCts?.Cancel();
            try
            {
                _hookThread?.Join(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore
            }

            _foregroundCts?.Dispose();
            _foregroundCts = null;
            _foregroundTask = null;
            _hookThread = null;
            _hookThreadId = 0;
            throw new InvalidOperationException("Global input hooks could not be installed (keyboard/mouse).");
        }
    }

    public RecordingEngineResult Stop()
    {
        _running = false;
        lock (_lock)
            _onEventRecorded = null;
        _foregroundCts?.Cancel();
        try
        {
            _foregroundTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore
        }

        if (_hookThreadId != 0)
            NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, 0, nint.Zero);

        _hookThread?.Join(TimeSpan.FromSeconds(5));
        _hookThread = null;
        _hookThreadId = 0;

        _foregroundCts?.Dispose();
        _foregroundCts = null;
        _foregroundTask = null;

        var env = RecordingEnvironmentCapture.Capture();
        List<RecordedInputEvent> copy;
        lock (_lock)
        {
            copy = _events.OrderBy(recordedEvent => recordedEvent.Sequence).ToList();
            _events.Clear();
        }

        _hooksReady = false;
        return new RecordingEngineResult(copy, env, _useFocusBoundMouseCoordinates);
    }

    public void Dispose() => Stop();

    private void ForegroundLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _running)
        {
            try
            {
                Thread.Sleep(120);
                RecordForegroundDelta(NativeMethods.GetForegroundWindow());
            }
            catch
            {
                // ignore polling errors
            }
        }
    }

    private void RecordForegroundDelta(nint newForegroundWindow)
    {
        if (newForegroundWindow == _lastForeground)
            return;

        var previousForegroundWindow = _lastForeground;
        if (previousForegroundWindow == nint.Zero)
        {
            _lastForeground = newForegroundWindow;
            if (newForegroundWindow == nint.Zero)
                return;

            AppendStamped(BuildFocusEvent(newForegroundWindow));
            return;
        }

        if (newForegroundWindow == nint.Zero)
        {
            AppendStamped(BuildFocusLostEvent());
            _lastForeground = nint.Zero;
            return;
        }

        _lastForeground = newForegroundWindow;
        AppendStamped(BuildFocusEvent(newForegroundWindow));
    }

    private static FocusChangedRecordedEvent BuildFocusLostEvent() =>
        new()
        {
            DelayBefore = default,
            Sequence = 0,
            Hwnd = null,
            WindowTitle = "",
            ProcessName = "",
            ProcessId = null,
            ReferenceClientWidth = null,
            ReferenceClientHeight = null
        };

    private FocusChangedRecordedEvent BuildFocusEvent(nint windowHandle)
    {
        var windowTitleBuilder = new StringBuilder(512);
        _ = NativeMethods.GetWindowText(windowHandle, windowTitleBuilder, windowTitleBuilder.Capacity);
        var windowTitle = windowTitleBuilder.ToString();
        NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        string processName = "";
        try
        {
            using var foregroundProcess = Process.GetProcessById((int)processId);
            processName = foregroundProcess.ProcessName;
        }
        catch
        {
            processName = "";
        }

        int? referenceClientWidth = null;
        int? referenceClientHeight = null;
        if (_useFocusBoundMouseCoordinates
            && NativeMethods.GetClientRect(windowHandle, out var clientRect))
        {
            referenceClientWidth = clientRect.Width;
            referenceClientHeight = clientRect.Height;
        }

        return new FocusChangedRecordedEvent
        {
            DelayBefore = default,
            Sequence = 0,
            Hwnd = (ulong)windowHandle,
            WindowTitle = windowTitle,
            ProcessName = processName,
            ProcessId = processId,
            ReferenceClientWidth = referenceClientWidth,
            ReferenceClientHeight = referenceClientHeight
        };
    }

    private void HookThreadProc(ManualResetEventSlim ready)
    {
        _hookThreadId = NativeMethods.GetCurrentThreadId();
        _stopwatch = Stopwatch.StartNew();

        var module = NativeMethods.GetModuleHandle(null);
        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _keyboardProc,
            module,
            0);
        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _mouseProc,
            module,
            0);

        if (_keyboardHook == nint.Zero || _mouseHook == nint.Zero)
        {
            _running = false;
            _onEventRecorded = null;
            _hooksReady = false;
            ready.Set();
            return;
        }

        _hooksReady = true;
        ready.Set();

        int getMessageResult;
        while ((getMessageResult = NativeMethods.GetMessage(out var windowsMessage, nint.Zero, 0, 0)) != 0)
        {
            if (getMessageResult == -1)
                break;
            _ = NativeMethods.TranslateMessage(ref windowsMessage);
            _ = NativeMethods.DispatchMessage(ref windowsMessage);
        }

        if (_keyboardHook != nint.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = nint.Zero;
        }

        if (_mouseHook != nint.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = nint.Zero;
        }

        _stopwatch?.Stop();
    }

    private nint KeyboardHook(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && _running)
        {
            var keyboardLowLevel = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var mappedEvent = HookToDomainMapper.FromKeyboard((int)(nint)wParam, keyboardLowLevel);
            if (mappedEvent is not null)
                AppendStamped(mappedEvent);
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private nint MouseHook(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && _running)
        {
            var mouseLowLevel = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var mappedEvent = HookToDomainMapper.FromMouse((int)(nint)wParam, mouseLowLevel);
            if (mappedEvent is not null)
                AppendStamped(mappedEvent);
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static bool ShouldRecordForegroundChangeBeforeEvent(RecordedInputEvent eventTemplate) =>
        eventTemplate is MouseMoveRecordedEvent
            or MouseButtonDownRecordedEvent
            or MouseButtonUpRecordedEvent
            or MouseWheelRecordedEvent
            or KeyDownRecordedEvent
            or KeyUpRecordedEvent;

    /// <summary>When focus-bound, inserts a <see cref="FocusChangedRecordedEvent"/> before input if
    /// <see cref="NativeMethods.GetForegroundWindow"/> changed (avoids losing focus vs. mouse order vs. the 120 ms poll).</summary>
    private void EnsureForegroundFocusChangeRecorded()
    {
        if (!_running)
            return;

        RecordForegroundDelta(NativeMethods.GetForegroundWindow());
    }

    private void AppendStamped(RecordedInputEvent eventTemplate)
    {
        if (_useFocusBoundMouseCoordinates && ShouldRecordForegroundChangeBeforeEvent(eventTemplate))
            EnsureForegroundFocusChangeRecorded();

        eventTemplate = ConvertMouseScreenToClientIfNeeded(eventTemplate);
        Action<RecordedInputEvent>? live;
        List<RecordedInputEvent> pendingCallbacks = new(2);
        lock (_lock)
        {
            live = _onEventRecorded;

            if (!_recordMouseMoves && eventTemplate is MouseMoveRecordedEvent)
                return;

            if (eventTemplate is KeyDownRecordedEvent keyDownAutorepeat
                && IsKeyboardAutorepeatKeyDown(_events, keyDownAutorepeat))
                return;

            if (eventTemplate is MouseMoveRecordedEvent mouseMoveEvent && _recordMouseMoves)
            {
                if (_haveLastRecordedMouseMove)
                {
                    if (mouseMoveEvent.ScreenX == _lastRecordedMouseMoveX &&
                        mouseMoveEvent.ScreenY == _lastRecordedMouseMoveY)
                        return;

                    var dx = mouseMoveEvent.ScreenX - _lastRecordedMouseMoveX;
                    var dy = mouseMoveEvent.ScreenY - _lastRecordedMouseMoveY;
                    var minSq = (long)_mouseMoveMinPixelDelta * _mouseMoveMinPixelDelta;
                    if ((long)dx * dx + (long)dy * dy < minSq)
                        return;
                }
            }

            var elapsed = _stopwatch?.Elapsed ?? TimeSpan.Zero;

            if (!_recordMouseMoves && IsAnchorEvent(eventTemplate) && _haveAnchor)
            {
                var gap = elapsed - _lastAnchorElapsed;
                if (gap >= MinAnchorGap)
                {
                    var waitDelayBefore = elapsed - _playbackTimelineEnd;
                    if (waitDelayBefore < TimeSpan.Zero)
                        waitDelayBefore = TimeSpan.Zero;

                    var waitSequence = (ulong)Interlocked.Increment(ref _sequence);
                    var waitStamped = Stamp(
                        new SyntheticWaitRecordedEvent
                        {
                            DelayBefore = default,
                            Sequence = 0,
                            AdditionalDelay = gap
                        },
                        waitDelayBefore,
                        waitSequence);
                    _events.Add(waitStamped);
                    pendingCallbacks.Add(waitStamped);
                    _playbackTimelineEnd += waitDelayBefore + gap;
                }
            }

            var delayBefore = elapsed - _playbackTimelineEnd;
            if (delayBefore < TimeSpan.Zero)
                delayBefore = TimeSpan.Zero;

            var nextSequence = (ulong)Interlocked.Increment(ref _sequence);
            var stampedEvent = Stamp(eventTemplate, delayBefore, nextSequence);
            _events.Add(stampedEvent);
            if (_useFocusBoundMouseCoordinates && stampedEvent is FocusChangedRecordedEvent appendedFocus)
                _useClientSpaceForMouse = appendedFocus.Hwnd is not null;

            pendingCallbacks.Add(stampedEvent);
            _playbackTimelineEnd += delayBefore;
            if (stampedEvent is SyntheticWaitRecordedEvent syntheticAfterMain)
                _playbackTimelineEnd += syntheticAfterMain.AdditionalDelay;

            if (stampedEvent is MouseMoveRecordedEvent storedMove && _recordMouseMoves)
            {
                _lastRecordedMouseMoveX = storedMove.ScreenX;
                _lastRecordedMouseMoveY = storedMove.ScreenY;
                _haveLastRecordedMouseMove = true;
            }

            if (!_recordMouseMoves && IsAnchorEvent(eventTemplate))
            {
                _haveAnchor = true;
                _lastAnchorElapsed = elapsed;
            }
        }

        foreach (var recordedEvent in pendingCallbacks)
            live?.Invoke(CloneForCallback(recordedEvent));
    }

    private static bool IsAnchorEvent(RecordedInputEvent recordedEvent) =>
        recordedEvent is KeyDownRecordedEvent
            or KeyUpRecordedEvent
            or MouseButtonDownRecordedEvent
            or MouseButtonUpRecordedEvent;

    /// <summary>
    /// WH_KEYBOARD_LL sends repeated <see cref="KeyDownRecordedEvent"/> for the same VK while the key is held.
    /// We keep only the first key-down per hold; repeats are not stored so the timeline shows one action until <see cref="KeyUpRecordedEvent"/>.
    /// </summary>
    private static bool IsKeyboardAutorepeatKeyDown(IReadOnlyList<RecordedInputEvent> events, KeyDownRecordedEvent keyDown)
    {
        for (var i = events.Count - 1; i >= 0; i--)
        {
            switch (events[i])
            {
                case SyntheticWaitRecordedEvent:
                    continue;
                case KeyDownRecordedEvent previousKeyDown:
                    return previousKeyDown.Vk == keyDown.Vk;
                default:
                    return false;
            }
        }

        return false;
    }

    private static RecordedInputEvent CloneForCallback(RecordedInputEvent recordedEvent) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { },
            KeyUpRecordedEvent keyUp => keyUp with { },
            MouseMoveRecordedEvent mouseMove => mouseMove with { },
            MouseButtonDownRecordedEvent mouseButtonDown => mouseButtonDown with { },
            MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with { },
            MouseWheelRecordedEvent mouseWheel => mouseWheel with { },
            FocusChangedRecordedEvent focusChanged => focusChanged with { },
            SyntheticWaitRecordedEvent syntheticWait => syntheticWait with { },
            _ => recordedEvent
        };

    /// <summary>When focus-bound recording is on, mouse X/Y are global screen coordinates until a window focus row;
    /// after that they are client coordinates relative to <see cref="NativeMethods.GetForegroundWindow"/> at capture time
    /// (after any inserted focus row).</summary>
    private RecordedInputEvent ConvertMouseScreenToClientIfNeeded(RecordedInputEvent eventTemplate)
    {
        if (!_useFocusBoundMouseCoordinates || !_useClientSpaceForMouse)
            return eventTemplate;

        var foregroundHwnd = NativeMethods.GetForegroundWindow();
        if (foregroundHwnd == nint.Zero)
            return eventTemplate;

        return eventTemplate switch
        {
            MouseMoveRecordedEvent mouseMove => ApplyClient(foregroundHwnd, mouseMove, mouseMove.ScreenX, mouseMove.ScreenY),
            MouseButtonDownRecordedEvent mouseButtonDown =>
                ApplyClient(foregroundHwnd, mouseButtonDown, mouseButtonDown.ScreenX, mouseButtonDown.ScreenY),
            MouseButtonUpRecordedEvent mouseButtonUp =>
                ApplyClient(foregroundHwnd, mouseButtonUp, mouseButtonUp.ScreenX, mouseButtonUp.ScreenY),
            MouseWheelRecordedEvent mouseWheel =>
                ApplyClient(foregroundHwnd, mouseWheel, mouseWheel.ScreenX, mouseWheel.ScreenY),
            _ => eventTemplate
        };
    }

    private static MouseMoveRecordedEvent ApplyClient(nint hwnd, MouseMoveRecordedEvent mouseMove, int screenX, int screenY)
    {
        var (cx, cy) = ScreenToClientOrScreen(hwnd, screenX, screenY);
        return mouseMove with { ScreenX = cx, ScreenY = cy };
    }

    private static MouseButtonDownRecordedEvent ApplyClient(nint hwnd, MouseButtonDownRecordedEvent e, int screenX, int screenY)
    {
        var (cx, cy) = ScreenToClientOrScreen(hwnd, screenX, screenY);
        return e with { ScreenX = cx, ScreenY = cy };
    }

    private static MouseButtonUpRecordedEvent ApplyClient(nint hwnd, MouseButtonUpRecordedEvent e, int screenX, int screenY)
    {
        var (cx, cy) = ScreenToClientOrScreen(hwnd, screenX, screenY);
        return e with { ScreenX = cx, ScreenY = cy };
    }

    private static MouseWheelRecordedEvent ApplyClient(nint hwnd, MouseWheelRecordedEvent e, int screenX, int screenY)
    {
        var (cx, cy) = ScreenToClientOrScreen(hwnd, screenX, screenY);
        return e with { ScreenX = cx, ScreenY = cy };
    }

    private static (int X, int Y) ScreenToClientOrScreen(nint hwnd, int screenX, int screenY)
    {
        var point = new NativeMethods.POINT { X = screenX, Y = screenY };
        if (!NativeMethods.ScreenToClient(hwnd, ref point))
            return (screenX, screenY);
        return (point.X, point.Y);
    }

    private static RecordedInputEvent Stamp(RecordedInputEvent recordedEvent, TimeSpan delayBefore, ulong sequence) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { DelayBefore = delayBefore, Sequence = sequence },
            KeyUpRecordedEvent keyUp => keyUp with { DelayBefore = delayBefore, Sequence = sequence },
            MouseMoveRecordedEvent mouseMove => mouseMove with { DelayBefore = delayBefore, Sequence = sequence },
            MouseButtonDownRecordedEvent mouseButtonDown =>
                mouseButtonDown with { DelayBefore = delayBefore, Sequence = sequence },
            MouseButtonUpRecordedEvent mouseButtonUp =>
                mouseButtonUp with { DelayBefore = delayBefore, Sequence = sequence },
            MouseWheelRecordedEvent mouseWheel =>
                mouseWheel with { DelayBefore = delayBefore, Sequence = sequence },
            FocusChangedRecordedEvent focusChanged =>
                focusChanged with { DelayBefore = delayBefore, Sequence = sequence },
            SyntheticWaitRecordedEvent syntheticWait =>
                syntheticWait with { DelayBefore = delayBefore, Sequence = sequence },
            _ => recordedEvent
        };
}
