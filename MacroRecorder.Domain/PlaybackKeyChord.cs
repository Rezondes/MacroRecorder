namespace MacroRecorder.Domain;

/// <summary>Global playback shortcut: Win32 virtual key plus modifier flags (for <c>RegisterHotKey</c>).</summary>
public readonly record struct PlaybackKeyChord(
    bool Ctrl,
    bool Alt,
    bool Shift,
    bool Win,
    uint VirtualKey)
{
    public bool HasNonModifierKey => VirtualKey != 0;
}
