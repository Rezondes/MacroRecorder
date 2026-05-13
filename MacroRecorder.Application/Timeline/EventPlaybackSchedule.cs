using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>
/// Computes absolute <c>WaitUntil</c> targets and edits from per-event <see cref="RecordedInputEvent.DelayBefore"/>
/// plus <see cref="SyntheticWaitRecordedEvent.AdditionalDelay"/> (same rules as recording / playback).
/// </summary>
public static class EventPlaybackSchedule
{
    public static TimeSpan GetWaitUntilTarget<T>(IReadOnlyList<T> ordered, int index)
        where T : RecordedInputEvent
    {
        if ((uint)index >= (uint)ordered.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var cursor = TimeSpan.Zero;
        for (var i = 0; i <= index; i++)
        {
            cursor += ordered[i].DelayBefore;
            if (i == index)
                return cursor;
            if (ordered[i] is SyntheticWaitRecordedEvent sw)
                cursor += sw.AdditionalDelay;
        }

        throw new InvalidOperationException("Unreachable.");
    }

    /// <summary>Wall-clock playback position immediately after <paramref name="index"/> has finished.</summary>
    public static TimeSpan PlaybackEndAfterEventAtIndex<T>(IReadOnlyList<T> ordered, int index)
        where T : RecordedInputEvent
    {
        var cursor = TimeSpan.Zero;
        for (var i = 0; i <= index; i++)
        {
            cursor += ordered[i].DelayBefore;
            if (ordered[i] is SyntheticWaitRecordedEvent sw)
                cursor += sw.AdditionalDelay;
        }

        return cursor;
    }

    /// <summary>Playback span from first to last event in an ordered sub-list (e.g. mouse path moves only).</summary>
    public static TimeSpan PlaybackSpanOverSublist<T>(IReadOnlyList<T> slice)
        where T : RecordedInputEvent
    {
        if (slice.Count <= 1)
            return TimeSpan.Zero;
        return GetWaitUntilTarget(slice, slice.Count - 1) - GetWaitUntilTarget(slice, 0);
    }

    public static TimeSpan[] ComputeWaitUntilTargets<T>(IReadOnlyList<T> ordered)
        where T : RecordedInputEvent
    {
        var targets = new TimeSpan[ordered.Count];
        var cursor = TimeSpan.Zero;
        for (var i = 0; i < ordered.Count; i++)
        {
            cursor += ordered[i].DelayBefore;
            targets[i] = cursor;
            if (ordered[i] is SyntheticWaitRecordedEvent sw)
                cursor += sw.AdditionalDelay;
        }

        return targets;
    }

    public static void ApplyWaitUntilTargets(List<RecordedInputEvent> ordered, TimeSpan[] targets)
    {
        if (ordered.Count != targets.Length)
            throw new ArgumentException("Length mismatch.", nameof(targets));

        for (var i = 0; i < ordered.Count; i++)
        {
            var delayBefore = i == 0
                ? targets[0]
                : targets[i] - targets[i - 1] - (ordered[i - 1] is SyntheticWaitRecordedEvent sw
                    ? sw.AdditionalDelay
                    : TimeSpan.Zero);
            if (delayBefore < TimeSpan.Zero)
                delayBefore = TimeSpan.Zero;
            ordered[i] = WithDelayBefore(ordered[i], delayBefore);
        }
    }

    public static void ShiftWaitTargetsEarlierFromIndex(List<RecordedInputEvent> ordered, int fromIndexInclusive,
        TimeSpan amount)
    {
        if (amount <= TimeSpan.Zero || fromIndexInclusive < 0 || fromIndexInclusive >= ordered.Count)
            return;

        var targets = ComputeWaitUntilTargets(ordered);
        for (var i = fromIndexInclusive; i < targets.Length; i++)
        {
            targets[i] -= amount;
            if (targets[i] < TimeSpan.Zero)
                targets[i] = TimeSpan.Zero;
        }

        ApplyWaitUntilTargets(ordered, targets);
    }

    public static void ShiftWaitTargetsLaterFromIndex(List<RecordedInputEvent> ordered, int fromIndexInclusive,
        TimeSpan delta)
    {
        if (delta == TimeSpan.Zero || fromIndexInclusive < 0 || fromIndexInclusive >= ordered.Count)
            return;

        var targets = ComputeWaitUntilTargets(ordered);
        for (var i = fromIndexInclusive; i < targets.Length; i++)
            targets[i] += delta;

        ApplyWaitUntilTargets(ordered, targets);
    }

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
