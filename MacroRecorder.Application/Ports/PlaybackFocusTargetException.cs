namespace MacroRecorder.Application.Ports;

/// <summary>Playback cannot run because the focus-bound target window is missing or its client size does not match the recording anchor.</summary>
public sealed class PlaybackFocusTargetException : InvalidOperationException
{
    public PlaybackFocusTargetException(
        PlaybackFocusTargetKind kind,
        string? processName = null,
        string? windowTitle = null,
        int? expectedClientWidth = null,
        int? expectedClientHeight = null,
        int? actualClientWidth = null,
        int? actualClientHeight = null)
        : base("Playback focus target verification failed.")
    {
        Kind = kind;
        ProcessName = processName;
        WindowTitle = windowTitle;
        ExpectedClientWidth = expectedClientWidth;
        ExpectedClientHeight = expectedClientHeight;
        ActualClientWidth = actualClientWidth;
        ActualClientHeight = actualClientHeight;
    }

    public PlaybackFocusTargetKind Kind { get; }

    public string? ProcessName { get; }

    public string? WindowTitle { get; }

    public int? ExpectedClientWidth { get; }

    public int? ExpectedClientHeight { get; }

    public int? ActualClientWidth { get; }

    public int? ActualClientHeight { get; }
}
