namespace MacroRecorder.Application.MacroQueue;

/// <summary>Optional pause between queue actions (manual resume).</summary>
public interface IMacroQueuePauseSignal
{
    /// <summary>When paused, blocks until resumed or <paramref name="cancellationToken"/> is cancelled.</summary>
    Task WaitIfPausedAsync(CancellationToken cancellationToken);
}
