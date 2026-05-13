namespace MacroRecorder.Application.Ports;

/// <summary>
/// Playback stopped because the user cancelled from the in-app overlay (e.g. during start delay),
/// not because of physical keyboard or mouse input during playback.
/// </summary>
public sealed class PlaybackAbortedByUserRequestException : OperationCanceledException
{
    public PlaybackAbortedByUserRequestException()
        : base("Playback was cancelled from the app overlay.")
    {
    }
}
