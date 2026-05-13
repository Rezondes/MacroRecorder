using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>Migrates pre–schema-2 macro JSON that stored <c>elapsedSinceSessionStart</c> per event.</summary>
public static class LegacyElapsedTimingMigration
{
    /// <summary>
    /// Fills <see cref="RecordedInputEvent.DelayBefore"/> from parallel legacy wait-until timestamps (same values
    /// that used to be stored as <c>elapsedSinceSessionStart</c>).
    /// </summary>
    public static void ApplyDelaysFromLegacyWaitUntilTimes(
        List<RecordedInputEvent> events,
        IReadOnlyList<TimeSpan> legacyWaitUntilPerEvent)
    {
        if (events.Count != legacyWaitUntilPerEvent.Count)
            throw new ArgumentException("Event list and legacy timestamp list must have the same length.");

        var playbackEnd = TimeSpan.Zero;
        for (var i = 0; i < events.Count; i++)
        {
            var legacyTarget = legacyWaitUntilPerEvent[i];
            var delayBefore = legacyTarget - playbackEnd;
            if (delayBefore < TimeSpan.Zero)
                delayBefore = TimeSpan.Zero;

            events[i] = WithDelayBefore(events[i], delayBefore);

            playbackEnd += delayBefore;
            if (events[i] is SyntheticWaitRecordedEvent sw)
                playbackEnd += sw.AdditionalDelay;
            else
                playbackEnd = Max(playbackEnd, legacyTarget);
        }
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;

    private static RecordedInputEvent WithDelayBefore(RecordedInputEvent recordedEvent, TimeSpan delayBefore) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { DelayBefore = delayBefore },
            KeyUpRecordedEvent keyUp => keyUp with { DelayBefore = delayBefore },
            MouseMoveRecordedEvent mouseMove => mouseMove with { DelayBefore = delayBefore },
            MouseButtonDownRecordedEvent mouseButtonDown =>
                mouseButtonDown with { DelayBefore = delayBefore },
            MouseButtonUpRecordedEvent mouseButtonUp =>
                mouseButtonUp with { DelayBefore = delayBefore },
            MouseWheelRecordedEvent mouseWheel => mouseWheel with { DelayBefore = delayBefore },
            FocusChangedRecordedEvent focusChanged => focusChanged with { DelayBefore = delayBefore },
            SyntheticWaitRecordedEvent syntheticWait =>
                syntheticWait with { DelayBefore = delayBefore },
            _ => throw new InvalidOperationException($"Unsupported event type: {recordedEvent.GetType().Name}")
        };
}
