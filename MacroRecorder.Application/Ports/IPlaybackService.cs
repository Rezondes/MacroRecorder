using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IPlaybackService
{
    /// <param name="userInputInterruptGraceMilliseconds">Wait this many milliseconds before playing events; until then physical input does not cancel playback (0 = off).</param>
    /// <param name="playbackFocusBringWindowToForeground">When a focus-changed event is played, call <c>SetForegroundWindow</c> for the target.</param>
    /// <param name="playbackFocusRestoreIfMinimized">When a focus-changed event is played, restore the target window if minimized.</param>
    Task PlayAsync(
        Macro macro,
        CancellationToken cancellationToken = default,
        int userInputInterruptGraceMilliseconds = 0,
        bool playbackFocusBringWindowToForeground = true,
        bool playbackFocusRestoreIfMinimized = true);

    /// <summary>Cancels in-progress playback, including during the start delay; no-op if nothing is playing.</summary>
    void RequestUserCancel();
}
