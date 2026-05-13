using System.Runtime.InteropServices;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.App.Services;

/// <summary>Registers global play hotkeys on the main window and dispatches to <see cref="MainViewModel"/>.</summary>
public sealed class MacroPlaybackHotkeyRegistrar
{
    private readonly Lazy<MainViewModel> _mainViewModel;
    private readonly RecordingCoordinator _recordingCoordinator;

    private readonly object _gate = new();
    private nint _hwnd;
    private int _nextHotKeyId = 1;
    private readonly Dictionary<int, MacroId> _hotKeyIdToMacroId = new();
    private int _suspendCount;

    public MacroPlaybackHotkeyRegistrar(Lazy<MainViewModel> mainViewModel, RecordingCoordinator recordingCoordinator)
    {
        _mainViewModel = mainViewModel;
        _recordingCoordinator = recordingCoordinator;
    }

    public void AttachWindow(nint windowHandle)
    {
        lock (_gate)
            _hwnd = windowHandle;
    }

    public void Suspend()
    {
        lock (_gate)
            _suspendCount++;
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_suspendCount > 0)
                _suspendCount--;
        }
    }

    public bool TryApplyAssignments(IReadOnlyDictionary<MacroId, PlaybackKeyChord> assignments, out int win32Error)
    {
        try
        {
            ApplyAssignments(assignments);
            win32Error = 0;
            return true;
        }
        catch (PlaybackHotkeyRegistrationException ex)
        {
            win32Error = ex.Win32Error;
            return false;
        }
    }

    /// <summary>Re-registers all hotkeys from the given map (replaces previous registrations).</summary>
    public void ApplyAssignments(IReadOnlyDictionary<MacroId, PlaybackKeyChord> assignments)
    {
        lock (_gate)
        {
            UnregisterAllLocked();

            if (_hwnd == 0 || assignments.Count == 0)
                return;

            foreach (var (macroId, chord) in assignments)
            {
                if (!chord.HasNonModifierKey)
                    continue;

                var id = _nextHotKeyId++;

                var mods = ToFsModifiers(chord);
                if (!User32Hotkeys.RegisterHotKey(_hwnd, id, mods, chord.VirtualKey))
                {
                    var err = Marshal.GetLastWin32Error();
                    UnregisterAllLocked();
                    throw new PlaybackHotkeyRegistrationException(
                        $"RegisterHotKey failed (Win32 {err}).",
                        err);
                }

                _hotKeyIdToMacroId[id] = macroId;
            }
        }
    }

    public bool TryConsumeWmHotKey(nint wParam)
    {
        MacroId macroId;
        lock (_gate)
        {
            if (_suspendCount > 0 || _recordingCoordinator.IsRecording)
                return _hotKeyIdToMacroId.ContainsKey((int)(nint)wParam);

            if (!_hotKeyIdToMacroId.TryGetValue((int)(nint)wParam, out macroId))
                return false;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
            return true;

        dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Normal,
            new Action(() => _ = _mainViewModel.Value.PlayMacroByIdAsync(macroId)));
        return true;
    }

    private void UnregisterAllLocked()
    {
        if (_hwnd == 0)
        {
            _hotKeyIdToMacroId.Clear();
            _nextHotKeyId = 1;
            return;
        }

        foreach (var id in _hotKeyIdToMacroId.Keys.ToArray())
            User32Hotkeys.UnregisterHotKey(_hwnd, id);
        _hotKeyIdToMacroId.Clear();
        _nextHotKeyId = 1;
    }

    private static uint ToFsModifiers(PlaybackKeyChord chord)
    {
        uint m = User32Hotkeys.ModNorepeat;
        if (chord.Ctrl)
            m |= User32Hotkeys.ModControl;
        if (chord.Alt)
            m |= User32Hotkeys.ModAlt;
        if (chord.Shift)
            m |= User32Hotkeys.ModShift;
        if (chord.Win)
            m |= User32Hotkeys.ModWin;
        return m;
    }
}
