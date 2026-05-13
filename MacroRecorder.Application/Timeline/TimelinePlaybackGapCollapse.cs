using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>
/// After deleting events from a timeline, the first survivor can still have a <see cref="RecordedInputEvent.DelayBefore"/>
/// that was sized for removed neighbors. This type computes how much absolute schedule time was removed and shifts
/// later <c>WaitUntil</c> targets earlier by subtracting that gap from the first survivor onward.
/// </summary>
public static class TimelinePlaybackGapCollapse
{
    /// <summary>Same as <see cref="EventPlaybackSchedule.PlaybackEndAfterEventAtIndex"/> for convenience.</summary>
    public static TimeSpan PlaybackEndAfterEvent(IReadOnlyList<RecordedInputEvent> ordered, int index) =>
        EventPlaybackSchedule.PlaybackEndAfterEventAtIndex(ordered, index);

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
            ? PlaybackEndAfterEvent(listBeforeRemoval, removeStartInclusive - 1)
            : TimeSpan.Zero;
        var right = EventPlaybackSchedule.GetWaitUntilTarget(listBeforeRemoval, removeEndInclusive + 1);
        var gap = right - left;
        return gap < TimeSpan.Zero ? TimeSpan.Zero : gap;
    }

    /// <summary>
    /// Subtracts <paramref name="amount"/> from every <c>WaitUntil</c> target from <paramref name="fromIndexInclusive"/> onward.
    /// </summary>
    public static void ShiftElapsedEarlierFromIndex(List<RecordedInputEvent> events, int fromIndexInclusive,
        TimeSpan amount) =>
        EventPlaybackSchedule.ShiftWaitTargetsEarlierFromIndex(events, fromIndexInclusive, amount);

    /// <summary>
    /// Adds <paramref name="delta"/> to every <c>WaitUntil</c> target from <paramref name="fromIndexInclusive"/> onward.
    /// Used when inserting a synthetic wait so the following schedule is not left unchanged while the inserted sleep
    /// adds duration.
    /// </summary>
    public static void ShiftElapsedFromIndex(List<RecordedInputEvent> events, int fromIndexInclusive, TimeSpan delta) =>
        EventPlaybackSchedule.ShiftWaitTargetsLaterFromIndex(events, fromIndexInclusive, delta);
}
