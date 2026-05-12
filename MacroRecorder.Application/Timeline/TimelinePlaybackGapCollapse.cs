using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>
/// After deleting events from a <b>normalized</b> timeline, remaining events still carry old cumulative
/// <see cref="RecordedInputEvent.ElapsedSinceSessionStart"/> values. <see cref="TimelineNormalizer"/> would keep the
/// wall-time gap between the neighbor before the hole and the first survivor. This type subtracts that gap from all
/// following survivors so total playback matches user expectation (removed segment no longer consumes time).
/// </summary>
public static class TimelinePlaybackGapCollapse
{
    /// <summary>
    /// Wall-clock position just after <paramref name="recordedEvent"/> has been executed in playback
    /// (<c>WaitUntil</c> target plus synthetic-wait sleep).
    /// </summary>
    public static TimeSpan PlaybackEndAfterEvent(RecordedInputEvent recordedEvent)
    {
        var end = recordedEvent.ElapsedSinceSessionStart;
        if (recordedEvent is SyntheticWaitRecordedEvent syntheticWait)
            end += syntheticWait.AdditionalDelay;
        return end;
    }

    /// <summary>
    /// Playback time between the end of the event before <paramref name="removeStartInclusive"/> and the
    /// <c>WaitUntil</c> target of the first event after <paramref name="removeEndInclusive"/>. When the deleted
    /// range is a suffix, returns <see cref="TimeSpan.Zero"/> (no following timestamps to shift).
    /// </summary>
    public static TimeSpan ComputeGapToSubtractBeforeRemovingRange(
        IReadOnlyList<RecordedInputEvent> listBeforeRemoval,
        int removeStartInclusive,
        int removeEndInclusive)
    {
        var n = listBeforeRemoval.Count;
        if (n == 0 || removeStartInclusive < 0 || removeEndInclusive < removeStartInclusive ||
            removeEndInclusive >= n)
            return TimeSpan.Zero;

        if (removeEndInclusive >= n - 1)
            return TimeSpan.Zero;

        var left = removeStartInclusive > 0
            ? PlaybackEndAfterEvent(listBeforeRemoval[removeStartInclusive - 1])
            : TimeSpan.Zero;
        var right = listBeforeRemoval[removeEndInclusive + 1].ElapsedSinceSessionStart;
        var gap = right - left;
        return gap < TimeSpan.Zero ? TimeSpan.Zero : gap;
    }

    /// <summary>
    /// Subtracts <paramref name="amount"/> from <see cref="RecordedInputEvent.ElapsedSinceSessionStart"/> on every
    /// event at index <paramref name="fromIndexInclusive"/> or later (after a delete shrunk the list).
    /// </summary>
    public static void ShiftElapsedEarlierFromIndex(List<RecordedInputEvent> events, int fromIndexInclusive,
        TimeSpan amount)
    {
        if (amount <= TimeSpan.Zero || fromIndexInclusive < 0 || fromIndexInclusive >= events.Count)
            return;

        for (var i = fromIndexInclusive; i < events.Count; i++)
        {
            var newElapsed = events[i].ElapsedSinceSessionStart - amount;
            if (newElapsed < TimeSpan.Zero)
                newElapsed = TimeSpan.Zero;
            events[i] = WithElapsed(events[i], newElapsed);
        }
    }

    private static RecordedInputEvent WithElapsed(RecordedInputEvent recordedEvent, TimeSpan elapsed) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { ElapsedSinceSessionStart = elapsed },
            KeyUpRecordedEvent keyUp => keyUp with { ElapsedSinceSessionStart = elapsed },
            MouseMoveRecordedEvent mouseMove => mouseMove with { ElapsedSinceSessionStart = elapsed },
            MouseButtonDownRecordedEvent mouseButtonDown =>
                mouseButtonDown with { ElapsedSinceSessionStart = elapsed },
            MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with { ElapsedSinceSessionStart = elapsed },
            MouseWheelRecordedEvent mouseWheel => mouseWheel with { ElapsedSinceSessionStart = elapsed },
            FocusChangedRecordedEvent focusChanged => focusChanged with { ElapsedSinceSessionStart = elapsed },
            SyntheticWaitRecordedEvent syntheticWait =>
                syntheticWait with { ElapsedSinceSessionStart = elapsed },
            _ => throw new InvalidOperationException($"Unsupported event type: {recordedEvent.GetType().Name}")
        };
}
