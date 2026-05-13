using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>
/// Estimates wall-clock playback time until the last step finishes (last <c>WaitUntil</c> target plus any trailing
/// <see cref="SyntheticWaitRecordedEvent"/> sleep).
/// </summary>
public static class PlaybackDurationEstimator
{
    /// <param name="eventsInPlaybackOrder">Editor / capture order.</param>
    public static TimeSpan EstimateTotalPlaybackDuration(IReadOnlyList<RecordedInputEvent> eventsInPlaybackOrder)
    {
        if (eventsInPlaybackOrder.Count == 0)
            return TimeSpan.Zero;

        if (LooksLikeNormalizedTimeline(eventsInPlaybackOrder))
            return PlaybackEndFromNormalizedOrder(eventsInPlaybackOrder);

        var work = new List<RecordedInputEvent>(eventsInPlaybackOrder.Count);
        foreach (var e in eventsInPlaybackOrder)
            work.Add(CloneEvent(e));
        TimelineNormalizer.NormalizeInPlace(work);
        return PlaybackEndFromNormalizedOrder(work);
    }

    /// <summary>
    /// After <see cref="TimelineNormalizer.NormalizeInPlace"/>, <see cref="RecordedInputEvent.Sequence"/> is 1..n
    /// in list order and <c>WaitUntil</c> targets are non-decreasing.
    /// </summary>
    private static bool LooksLikeNormalizedTimeline(IReadOnlyList<RecordedInputEvent> events)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Sequence != (ulong)(i + 1))
                return false;
            if (i > 0 &&
                EventPlaybackSchedule.GetWaitUntilTarget(events, i) <
                EventPlaybackSchedule.GetWaitUntilTarget(events, i - 1))
                return false;
        }

        return true;
    }

    private static TimeSpan PlaybackEndFromNormalizedOrder(IReadOnlyList<RecordedInputEvent> ordered) =>
        EventPlaybackSchedule.PlaybackEndAfterEventAtIndex(ordered, ordered.Count - 1);

    private static RecordedInputEvent CloneEvent(RecordedInputEvent recordedEvent) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { },
            KeyUpRecordedEvent keyUp => keyUp with { },
            MouseMoveRecordedEvent mouseMove => mouseMove with { },
            MouseButtonDownRecordedEvent mouseButtonDown => mouseButtonDown with { },
            MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with { },
            MouseWheelRecordedEvent mouseWheel => mouseWheel with { },
            FocusChangedRecordedEvent focusChanged => focusChanged with { },
            SyntheticWaitRecordedEvent syntheticWait => syntheticWait with { },
            _ => throw new InvalidOperationException($"Unsupported event type: {recordedEvent.GetType().Name}")
        };
}
