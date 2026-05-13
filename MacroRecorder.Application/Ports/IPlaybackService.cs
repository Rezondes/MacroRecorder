using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IPlaybackService
{
    /// <param name="userInputInterruptGraceMilliseconds">Wait this many milliseconds before playing events; until then physical input does not cancel playback (0 = off).</param>
    Task PlayAsync(Macro macro, CancellationToken cancellationToken = default, int userInputInterruptGraceMilliseconds = 0);

    /// <summary>Cancels in-progress playback, including during the start delay; no-op if nothing is playing.</summary>
    void RequestUserCancel();
}
