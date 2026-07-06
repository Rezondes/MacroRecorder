using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Interop;
using Microsoft.Extensions.Logging;

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
    private volatile bool _haveLastRecordedMouseMove;
    private volatile int _lastRecordedMouseMoveX;
    private volatile int _lastRecordedMouseMoveY;
    private volatile bool _haveSecondLastRecordedMouseMove;
    private volatile int _secondLastRecordedMouseMoveX;
    private volatile int _secondLastRecordedMouseMoveY;
    private volatile bool _leftMouseButtonDown;
    private volatile bool _rightMouseButtonDown;
    private volatile bool _middleMouseButtonDown;
    private Action<RecordedInputEvent>? _onEventRecorded;
    private ConcurrentQueue<RecordedInputEvent>? _liveEventQueue;
    private Thread? _callbackThread;
    private volatile bool _callbackDispatchRunning;
    private AutoResetEvent? _callbackSignal;
    private readonly RecordingKeyboardHoldState _keyboardHoldState = new();
    private volatile bool _recordMouseMoves = true;
    private volatile int _mouseMoveMinPixelDelta = 5;
    private bool _haveAnchor;
    private TimeSpan _lastAnchorElapsed;
    private TimeSpan _playbackTimelineEnd;
    private bool _useFocusBoundMouseCoordinates;
    private volatile bool _useClientSpaceForMouse;
    private readonly ILogger<LowLevelRecordingEngine> _logger;

    public LowLevelRecordingEngine(ILogger<LowLevelRecordingEngine> logger)
    {
        _logger = logger;
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
            _haveSecondLastRecordedMouseMove = false;
            _leftMouseButtonDown = false;
            _rightMouseButtonDown = false;
            _middleMouseButtonDown = false;
            _recordMouseMoves = recordMouseMoves;
            _mouseMoveMinPixelDelta = clampedMinPixels;
            _haveAnchor = false;
            _lastAnchorElapsed = TimeSpan.Zero;
            _playbackTimelineEnd = TimeSpan.Zero;
            _useFocusBoundMouseCoordinates = useFocusBoundMouseCoordinates;
            _useClientSpaceForMouse = false;
            _keyboardHoldState.Reset();
        }

        _onEventRecorded = onEventRecorded;
        if (onEventRecorded is not null)
        {
            _liveEventQueue = new ConcurrentQueue<RecordedInputEvent>();
            _callbackSignal = new AutoResetEvent(false);
            _callbackDispatchRunning = true;
            _callbackThread = new Thread(CallbackDispatchLoop) { IsBackground = true, Name = "RecordingLiveCallback" };
            _callbackThread.Start();
        }
        else
        {
            _liveEventQueue = null;
            _callbackSignal = null;
            _callbackThread = null;
        }
        _hooksReady = false;
        _lastForeground = NormalizeToRootWindow(NativeMethods.GetForegroundWindow());
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
            StopCallbackDispatch();
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
            _logger.LogError("Global input hooks could not be installed (keyboard/mouse)");
            throw new InvalidOperationException("Global input hooks could not be installed (keyboard/mouse).");
        }
    }

    public RecordingEngineResult Stop()
    {
        _running = false;
        StopCallbackDispatch();
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

        var env = RecordingEnvironmentCapture.Capture(_logger);
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
                if (_useFocusBoundMouseCoordinates)
                    continue;

                RecordForegroundDelta(NativeMethods.GetForegroundWindow());
            }
            catch
            {
                // ignore polling errors
            }
        }
    }

    private static nint NormalizeToRootWindow(nint windowHandle)
    {
        if (windowHandle == nint.Zero || !NativeMethods.IsWindow(windowHandle))
            return nint.Zero;

        var rootWindow = NativeMethods.GetAncestor(windowHandle, NativeMethods.GA_ROOT);
        return rootWindow != nint.Zero ? rootWindow : windowHandle;
    }

    private void RecordForegroundDelta(nint newForegroundWindow)
    {
        newForegroundWindow = NormalizeToRootWindow(newForegroundWindow);

        if (_useFocusBoundMouseCoordinates && newForegroundWindow == nint.Zero)
            return;

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
            _logger.LogError("Hook thread failed to install keyboard or mouse hook");
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
            var windowMessage = (int)(nint)wParam;
            if (windowMessage == NativeMethods.WM_MOUSEMOVE && !_recordMouseMoves)
                return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

            var mouseLowLevel = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            if (windowMessage == NativeMethods.WM_MOUSEMOVE && _recordMouseMoves && !_useClientSpaceForMouse
                && ShouldSkipMouseMoveFast(mouseLowLevel.pt.X, mouseLowLevel.pt.Y))
                return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

            var mappedEvent = HookToDomainMapper.FromMouse(windowMessage, mouseLowLevel);
            if (mappedEvent is not null)
                AppendStamped(mappedEvent);
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private bool ShouldSkipMouseMoveFast(int screenX, int screenY) =>
        MouseMoveRecordingFilter.ShouldSkipMove(
            screenX,
            screenY,
            _lastRecordedMouseMoveX,
            _lastRecordedMouseMoveY,
            _haveLastRecordedMouseMove,
            _secondLastRecordedMouseMoveX,
            _secondLastRecordedMouseMoveY,
            _haveSecondLastRecordedMouseMove,
            _mouseMoveMinPixelDelta,
            _leftMouseButtonDown || _rightMouseButtonDown || _middleMouseButtonDown);

    private static bool ShouldRecordForegroundChangeBeforeKeyboardEvent(RecordedInputEvent eventTemplate) =>
        eventTemplate is KeyDownRecordedEvent or KeyUpRecordedEvent;

    /// <summary>When focus-bound, inserts a focus row for the window under the cursor before mouse down
    /// (clicks often change focus after mousedown; <see cref="NativeMethods.GetForegroundWindow"/> lags).</summary>
    private void EnsureFocusAtMouseTarget(int screenX, int screenY)
    {
        if (!_running || !_useFocusBoundMouseCoordinates)
            return;

        var point = new NativeMethods.POINT { X = screenX, Y = screenY };
        var windowAtPoint = NativeMethods.WindowFromPoint(point);
        if (windowAtPoint == nint.Zero)
            return;

        RecordForegroundDelta(windowAtPoint);
    }

    /// <summary>When focus-bound, inserts a <see cref="FocusChangedRecordedEvent"/> before keyboard input if
    /// <see cref="NativeMethods.GetForegroundWindow"/> changed (avoids losing focus vs. the 120 ms poll).</summary>
    private void EnsureForegroundFocusChangeRecorded()
    {
        if (!_running)
            return;

        RecordForegroundDelta(NativeMethods.GetForegroundWindow());
    }

    private void AppendStamped(RecordedInputEvent eventTemplate)
    {
        if (_useFocusBoundMouseCoordinates)
        {
            if (eventTemplate is MouseButtonDownRecordedEvent mouseButtonDown)
                EnsureFocusAtMouseTarget(mouseButtonDown.ScreenX, mouseButtonDown.ScreenY);
            else if (ShouldRecordForegroundChangeBeforeKeyboardEvent(eventTemplate))
                EnsureForegroundFocusChangeRecorded();
        }

        eventTemplate = ConvertMouseScreenToClientIfNeeded(eventTemplate);
        List<RecordedInputEvent> pendingCallbacks = new(2);
        if (!TryEnterAppendLock())
            return;

        try
        {
            if (_recordMouseMoves && TryGetAnchorMousePosition(eventTemplate, out var anchorScreenX, out var anchorScreenY))
                FlushMouseMoveAt(anchorScreenX, anchorScreenY, pendingCallbacks);

            if (!_recordMouseMoves && eventTemplate is MouseMoveRecordedEvent)
                return;

            if (eventTemplate is KeyDownRecordedEvent keyDownAutorepeat
                && _keyboardHoldState.IsAutorepeatKeyDown(keyDownAutorepeat.Vk))
                return;

            if (eventTemplate is MouseMoveRecordedEvent mouseMoveEvent && _recordMouseMoves
                && MouseMoveRecordingFilter.ShouldSkipMove(
                    mouseMoveEvent.ScreenX,
                    mouseMoveEvent.ScreenY,
                    _lastRecordedMouseMoveX,
                    _lastRecordedMouseMoveY,
                    _haveLastRecordedMouseMove,
                    _secondLastRecordedMouseMoveX,
                    _secondLastRecordedMouseMoveY,
                    _haveSecondLastRecordedMouseMove,
                    _mouseMoveMinPixelDelta,
                    _leftMouseButtonDown || _rightMouseButtonDown || _middleMouseButtonDown))
                return;

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
                            DelayBefore = default,
                            Sequence = 0,
                            AdditionalDelay = gap
                        },
                        TimeSpan.Zero,
                        waitSequence);
                    _events.Add(waitStamped);
                    pendingCallbacks.Add(waitStamped);
                    _playbackTimelineEnd += gap;
                }
            }

            StoreStampedEvent(eventTemplate, pendingCallbacks);
            UpdateKeyboardHoldState(eventTemplate);

            if (!_recordMouseMoves && IsAnchorEvent(eventTemplate))
            {
                _haveAnchor = true;
                _lastAnchorElapsed = elapsed;
            }

            UpdateMouseButtonState(eventTemplate);
        }
        finally
        {
            Monitor.Exit(_lock);
        }

        EnqueueLiveCallbacks(pendingCallbacks);
    }

    private bool TryEnterAppendLock()
    {
        if (NativeMethods.GetCurrentThreadId() == _hookThreadId)
        {
            Monitor.Enter(_lock);
            return true;
        }

        return Monitor.TryEnter(_lock);
    }

    private void CallbackDispatchLoop()
    {
        var signal = _callbackSignal;
        var queue = _liveEventQueue;
        if (signal is null || queue is null)
            return;

        while (_callbackDispatchRunning)
        {
            signal.WaitOne(50);
            DispatchQueuedLiveCallbacks(queue);
        }

        DispatchQueuedLiveCallbacks(queue);
    }

    private void DispatchQueuedLiveCallbacks(ConcurrentQueue<RecordedInputEvent> queue)
    {
        var live = _onEventRecorded;
        if (live is null)
            return;

        while (queue.TryDequeue(out var recordedEvent))
            live.Invoke(recordedEvent);
    }

    private void EnqueueLiveCallbacks(List<RecordedInputEvent> pendingCallbacks)
    {
        var queue = _liveEventQueue;
        var signal = _callbackSignal;
        if (queue is null || pendingCallbacks.Count == 0)
            return;

        foreach (var recordedEvent in pendingCallbacks)
            queue.Enqueue(CloneForCallback(recordedEvent));
        signal?.Set();
    }

    private void StopCallbackDispatch()
    {
        _callbackDispatchRunning = false;
        _callbackSignal?.Set();
        try
        {
            _callbackThread?.Join(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore
        }

        _callbackThread = null;
        _callbackSignal?.Dispose();
        _callbackSignal = null;
        _liveEventQueue = null;
    }

    private void UpdateKeyboardHoldState(RecordedInputEvent eventTemplate)
    {
        switch (eventTemplate)
        {
            case KeyDownRecordedEvent keyDown:
                _keyboardHoldState.OnKeyDownStored(keyDown.Vk);
                break;
            case KeyUpRecordedEvent keyUp:
                _keyboardHoldState.OnKeyUpStored(keyUp.Vk);
                break;
        }
    }

    private static bool TryGetAnchorMousePosition(RecordedInputEvent eventTemplate, out int screenX, out int screenY)
    {
        switch (eventTemplate)
        {
            case MouseButtonDownRecordedEvent mouseButtonDown:
                screenX = mouseButtonDown.ScreenX;
                screenY = mouseButtonDown.ScreenY;
                return true;
            case MouseButtonUpRecordedEvent mouseButtonUp:
                screenX = mouseButtonUp.ScreenX;
                screenY = mouseButtonUp.ScreenY;
                return true;
            case MouseWheelRecordedEvent mouseWheel:
                screenX = mouseWheel.ScreenX;
                screenY = mouseWheel.ScreenY;
                return true;
            default:
                screenX = 0;
                screenY = 0;
                return false;
        }
    }

    private void FlushMouseMoveAt(int screenX, int screenY, List<RecordedInputEvent> pendingCallbacks)
    {
        if (!_recordMouseMoves)
            return;

        if (_haveLastRecordedMouseMove
            && _lastRecordedMouseMoveX == screenX
            && _lastRecordedMouseMoveY == screenY)
            return;

        var moveEvent = new MouseMoveRecordedEvent
        {
            DelayBefore = default,
            Sequence = 0,
            ScreenX = screenX,
            ScreenY = screenY
        };
        StoreStampedEvent(moveEvent, pendingCallbacks);
    }

    private void StoreStampedEvent(RecordedInputEvent eventTemplate, List<RecordedInputEvent> pendingCallbacks)
    {
        var elapsed = _stopwatch?.Elapsed ?? TimeSpan.Zero;
        var delayBefore = RecordingTimelineDelay.ComputeDelayBefore(elapsed, _playbackTimelineEnd);
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
            RememberRecordedMouseMove(storedMove.ScreenX, storedMove.ScreenY);
    }

    private void RememberRecordedMouseMove(int screenX, int screenY)
    {
        if (_haveLastRecordedMouseMove)
        {
            _secondLastRecordedMouseMoveX = _lastRecordedMouseMoveX;
            _secondLastRecordedMouseMoveY = _lastRecordedMouseMoveY;
            _haveSecondLastRecordedMouseMove = true;
        }

        _lastRecordedMouseMoveX = screenX;
        _lastRecordedMouseMoveY = screenY;
        _haveLastRecordedMouseMove = true;
    }

    private void UpdateMouseButtonState(RecordedInputEvent eventTemplate)
    {
        switch (eventTemplate)
        {
            case MouseButtonDownRecordedEvent mouseButtonDown:
                SetMouseButtonDown(mouseButtonDown.Button, true);
                break;
            case MouseButtonUpRecordedEvent mouseButtonUp:
                SetMouseButtonDown(mouseButtonUp.Button, false);
                break;
        }
    }

    private void SetMouseButtonDown(MouseButtonKind button, bool isDown)
    {
        switch (button)
        {
            case MouseButtonKind.Left:
                _leftMouseButtonDown = isDown;
                break;
            case MouseButtonKind.Right:
                _rightMouseButtonDown = isDown;
                break;
            case MouseButtonKind.Middle:
                _middleMouseButtonDown = isDown;
                break;
        }
    }

    private static bool IsAnchorEvent(RecordedInputEvent recordedEvent) =>
        recordedEvent is KeyDownRecordedEvent
            or KeyUpRecordedEvent
            or MouseButtonDownRecordedEvent
            or MouseButtonUpRecordedEvent;

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
    /// after that they are client coordinates relative to the recorded focus window
    /// (after any inserted focus row).</summary>
    private RecordedInputEvent ConvertMouseScreenToClientIfNeeded(RecordedInputEvent eventTemplate)
    {
        if (!_useFocusBoundMouseCoordinates || !_useClientSpaceForMouse)
            return eventTemplate;

        var clientSpaceHwnd = _lastForeground != nint.Zero
            ? _lastForeground
            : NormalizeToRootWindow(NativeMethods.GetForegroundWindow());
        if (clientSpaceHwnd == nint.Zero)
            return eventTemplate;

        return eventTemplate switch
        {
            MouseMoveRecordedEvent mouseMove => ApplyClient(clientSpaceHwnd, mouseMove, mouseMove.ScreenX, mouseMove.ScreenY),
            MouseButtonDownRecordedEvent mouseButtonDown =>
                ApplyClient(clientSpaceHwnd, mouseButtonDown, mouseButtonDown.ScreenX, mouseButtonDown.ScreenY),
            MouseButtonUpRecordedEvent mouseButtonUp =>
                ApplyClient(clientSpaceHwnd, mouseButtonUp, mouseButtonUp.ScreenX, mouseButtonUp.ScreenY),
            MouseWheelRecordedEvent mouseWheel =>
                ApplyClient(clientSpaceHwnd, mouseWheel, mouseWheel.ScreenX, mouseWheel.ScreenY),
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
