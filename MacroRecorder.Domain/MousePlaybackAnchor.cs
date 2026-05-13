namespace MacroRecorder.Domain;

/// <summary>Window identity and client size captured when recording with focus-bound mouse coordinates.</summary>
public sealed record MousePlaybackAnchor(
    string ProcessName,
    string WindowTitle,
    int ReferenceClientWidth,
    int ReferenceClientHeight,
    ulong? RecordedHwndForDiagnostics = null);
