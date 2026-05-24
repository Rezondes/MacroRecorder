using MacroRecorder.Application.Timeline;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class TimelinePlaybackGapCollapseTests
{
    [Fact]
    public void ComputeGapToSubtractBeforeRemovingRange_returns_zero_for_suffix_deletion()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1, delayBefore: TimeSpan.FromMilliseconds(10)),
            TestEvents.KeyDown(sequence: 2, delayBefore: TimeSpan.FromMilliseconds(20))
        };

        var gap = TimelinePlaybackGapCollapse.ComputeGapToSubtractBeforeRemovingRange(events, 1, 1);

        Assert.Equal(TimeSpan.Zero, gap);
    }

    [Fact]
    public void ComputeGapToSubtractBeforeRemovingRange_measures_schedule_between_neighbors()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1, delayBefore: TimeSpan.FromMilliseconds(10)),
            TestEvents.KeyDown(sequence: 2, delayBefore: TimeSpan.FromMilliseconds(90)),
            TestEvents.KeyDown(sequence: 3, delayBefore: TimeSpan.FromMilliseconds(5))
        };

        var gap = TimelinePlaybackGapCollapse.ComputeGapToSubtractBeforeRemovingRange(events, 1, 1);

        Assert.Equal(TimeSpan.FromMilliseconds(95), gap);
    }

    [Fact]
    public void ShiftElapsedEarlierFromIndex_reduces_later_wait_targets()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1, delayBefore: TimeSpan.FromMilliseconds(10)),
            TestEvents.KeyDown(sequence: 2, delayBefore: TimeSpan.FromMilliseconds(50)),
            TestEvents.KeyDown(sequence: 3, delayBefore: TimeSpan.FromMilliseconds(5))
        };

        TimelinePlaybackGapCollapse.ShiftElapsedEarlierFromIndex(events, 1, TimeSpan.FromMilliseconds(20));

        Assert.Equal(TimeSpan.FromMilliseconds(45), EventPlaybackSchedule.GetWaitUntilTarget(events, 2));
    }
}
