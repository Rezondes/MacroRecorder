using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using MacroRecorder.Domain.MacroQueue;

namespace MacroRecorder.Application.MacroQueue;

public sealed class MacroQueueRunner(
    MacroWorkspaceService workspace,
    IPlaybackService playback,
    IMacroQueuePauseSignal? pauseSignal = null)
{
    private readonly IMacroQueuePauseSignal _pause = pauseSignal ?? NoMacroQueuePauseSignal.Instance;

    public async Task RunAsync(
        MacroQueueDocument document,
        int userInputInterruptGraceMilliseconds,
        bool playbackFocusBringWindowToForeground,
        bool playbackFocusRestoreIfMinimized,
        CancellationToken cancellationToken = default)
    {
        if (document.Steps.Count == 0)
            return;

        do
        {
            foreach (var step in document.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _pause.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                await DelayNonNegativeAsync(step.InitialDelay, cancellationToken).ConfigureAwait(false);

                for (var runIndex = 0; runIndex < step.RepeatCount; runIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _pause.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);

                    var macro = await workspace.GetAsync(step.MacroId, cancellationToken).ConfigureAwait(false);
                    if (macro is null)
                        throw new MacroQueueMissingMacroException(step.MacroId);
                    if (macro.Events.Count == 0)
                        throw new MacroQueueEmptyMacroException(step.MacroId);

                    await playback
                        .PlayAsync(
                            macro,
                            cancellationToken,
                            userInputInterruptGraceMilliseconds,
                            playbackFocusBringWindowToForeground,
                            playbackFocusRestoreIfMinimized)
                        .ConfigureAwait(false);

                    if (runIndex < step.RepeatCount - 1)
                        await DelayNonNegativeAsync(step.DelayBetweenRuns, cancellationToken).ConfigureAwait(false);
                }

                await _pause.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                await DelayNonNegativeAsync(step.PostStepDelay, cancellationToken).ConfigureAwait(false);
            }
        } while (document.LoopWholeQueue);
    }

    private static Task DelayNonNegativeAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
            return Task.CompletedTask;
        return Task.Delay(delay, cancellationToken);
    }

    /// <summary>Wall-clock estimate including macro playback durations and all delays; does not include loop repeats.</summary>
    public static TimeSpan EstimateTotalDuration(
        MacroQueueDocument document,
        IReadOnlyDictionary<MacroId, TimeSpan> macroPlaybackDurations)
    {
        var total = TimeSpan.Zero;
        foreach (var step in document.Steps)
        {
            total += step.InitialDelay;
            if (step.RepeatCount <= 0)
                continue;

            if (!macroPlaybackDurations.TryGetValue(step.MacroId, out var once))
                once = TimeSpan.Zero;

            total += TimeSpan.FromTicks(once.Ticks * step.RepeatCount);
            if (step.RepeatCount > 1)
                total += TimeSpan.FromTicks(step.DelayBetweenRuns.Ticks * (step.RepeatCount - 1));

            total += step.PostStepDelay;
        }

        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }

    /// <summary>Builds playback duration map from workspace summaries (same source as overview list).</summary>
    public static Dictionary<MacroId, TimeSpan> PlaybackDurationsFromSummaries(IEnumerable<MacroSummary> summaries)
    {
        var map = new Dictionary<MacroId, TimeSpan>();
        foreach (var summary in summaries)
            map[summary.Id] = summary.TotalPlaybackDuration;
        return map;
    }
}
