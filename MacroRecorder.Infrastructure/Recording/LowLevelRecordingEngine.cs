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
    private bool _haveAnchor;
    private TimeSpan _lastAnchorElapsed;

    public LowLevelRecordingEngine()
    {
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
    }

    public bool IsRunning => _running;

    public void Start(Action<RecordedInputEvent>? onEventRecorded = null, bool recordMouseMoves = true)
    {
        if (_running)
            throw new InvalidOperationException("Recording already running.");

        lock (_lock)
        {
            _events.Clear();
            Interlocked.Exchange(ref _sequence, 0);
            _haveLastRecordedMouseMove = false;
            _recordMouseMoves = recordMouseMoves;
            _haveAnchor = false;
            _lastAnchorElapsed = TimeSpan.Zero;
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
        return new RecordingEngineResult(copy, env);
    }

    public void Dispose() => Stop();

    private void ForegroundLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _running)
        {
            try
            {
                Thread.Sleep(120);
                var foregroundWindow = NativeMethods.GetForegroundWindow();
                if (foregroundWindow == nint.Zero || foregroundWindow == _lastForeground)
                    continue;

                var previousForegroundWindow = _lastForeground;
                _lastForeground = foregroundWindow;
                if (previousForegroundWindow == nint.Zero)
                    continue;

                var focusEvent = BuildFocusEvent(foregroundWindow);
                if (focusEvent is not null)
                    AppendStamped(focusEvent);
            }
            catch
            {
                // ignore polling errors
            }
        }
    }

    private static FocusChangedRecordedEvent? BuildFocusEvent(nint windowHandle)
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

        return new FocusChangedRecordedEvent
        {
            ElapsedSinceSessionStart = default,
            Sequence = 0,
            Hwnd = (ulong)windowHandle,
            WindowTitle = windowTitle,
            ProcessName = processName,
            ProcessId = processId
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

    private void AppendStamped(RecordedInputEvent eventTemplate)
    {
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
                if (_haveLastRecordedMouseMove &&
                    mouseMoveEvent.ScreenX == _lastRecordedMouseMoveX &&
                    mouseMoveEvent.ScreenY == _lastRecordedMouseMoveY)
                    return;

                _lastRecordedMouseMoveX = mouseMoveEvent.ScreenX;
                _lastRecordedMouseMoveY = mouseMoveEvent.ScreenY;
                _haveLastRecordedMouseMove = true;
            }

            var elapsed = _stopwatch?.Elapsed ?? TimeSpan.Zero;

            if (!_recordMouseMoves && IsAnchorEvent(eventTemplate) && _haveAnchor)
            {
                var gap = elapsed - _lastAnchorElapsed;
                if (gap >= MinAnchorGap)
                {
                    var waitSequence = (ulong)Interlocked.Increment(ref _sequence);
                    var waitStamped = Stamp(
                        new SyntheticWaitRecordedEvent
                        {
                            ElapsedSinceSessionStart = default,
                            Sequence = 0,
                            AdditionalDelay = gap
                        },
                        elapsed,
                        waitSequence);
                    _events.Add(waitStamped);
                    pendingCallbacks.Add(waitStamped);
                }
            }

            var nextSequence = (ulong)Interlocked.Increment(ref _sequence);
            var stampedEvent = Stamp(eventTemplate, elapsed, nextSequence);
            _events.Add(stampedEvent);
            pendingCallbacks.Add(stampedEvent);

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

    private static RecordedInputEvent Stamp(RecordedInputEvent recordedEvent, TimeSpan elapsed, ulong sequence) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            KeyUpRecordedEvent keyUp => keyUp with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseMoveRecordedEvent mouseMove => mouseMove with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseButtonDownRecordedEvent mouseButtonDown =>
                mouseButtonDown with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseButtonUpRecordedEvent mouseButtonUp =>
                mouseButtonUp with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseWheelRecordedEvent mouseWheel =>
                mouseWheel with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            FocusChangedRecordedEvent focusChanged =>
                focusChanged with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            SyntheticWaitRecordedEvent syntheticWait =>
                syntheticWait with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            _ => recordedEvent
        };
}
