namespace MacroRecorder.Application.Ports;

/// <summary>
/// Raised when macro playback was cancelled because the user produced real (non-synthetic) input.
/// </summary>
public sealed class PlaybackInterruptedByUserException : OperationCanceledException
{
    public PlaybackInterruptedByUserException()
        : base("Playback was cancelled because the user produced keyboard or mouse input.")
    {
    }
}
