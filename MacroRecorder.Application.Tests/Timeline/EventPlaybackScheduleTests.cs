using MacroRecorder.Application.Timeline;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class EventPlaybackScheduleTests
{
    [Fact]
    public void GetWaitUntilTarget_sums_delay_before_and_prior_synthetic_waits()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1, delayBefore: TimeSpan.FromMilliseconds(100)),
            TestEvents.SyntheticWait(2, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200)),
            TestEvents.KeyDown(sequence: 3, delayBefore: TimeSpan.FromMilliseconds(25))
        };

        Assert.Equal(TimeSpan.FromMilliseconds(100), EventPlaybackSchedule.GetWaitUntilTarget(events, 0));
        Assert.Equal(TimeSpan.FromMilliseconds(150), EventPlaybackSchedule.GetWaitUntilTarget(events, 1));
        Assert.Equal(TimeSpan.FromMilliseconds(375), EventPlaybackSchedule.GetWaitUntilTarget(events, 2));
    }

    [Fact]
    public void ComputeWaitUntilTargets_matches_per_index_targets()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1, delayBefore: TimeSpan.FromMilliseconds(10)),
            TestEvents.KeyDown(sequence: 2, delayBefore: TimeSpan.FromMilliseconds(20))
        };

        var targets = EventPlaybackSchedule.ComputeWaitUntilTargets(events);

        Assert.Equal(TimeSpan.FromMilliseconds(10), targets[0]);
        Assert.Equal(TimeSpan.FromMilliseconds(30), targets[1]);
    }

    [Fact]
    public void ApplyWaitUntilTargets_round_trips_compute()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1, delayBefore: TimeSpan.FromMilliseconds(10)),
            TestEvents.SyntheticWait(2, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(15)),
            TestEvents.KeyDown(sequence: 3, delayBefore: TimeSpan.FromMilliseconds(7))
        };
        var targets = EventPlaybackSchedule.ComputeWaitUntilTargets(events);

        EventPlaybackSchedule.ApplyWaitUntilTargets(events, targets);

        Assert.Equal(targets, EventPlaybackSchedule.ComputeWaitUntilTargets(events));
    }

    [Fact]
    public void GetWaitUntilTarget_throws_when_index_out_of_range()
    {
        var events = new List<RecordedInputEvent> { TestEvents.KeyDown() };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventPlaybackSchedule.GetWaitUntilTarget(events, 1));
    }

    [Fact]
    public void PlaybackEndAfterEventAtIndex_includes_trailing_synthetic_wait()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.SyntheticWait(1, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(40))
        };

        Assert.Equal(TimeSpan.FromMilliseconds(45), EventPlaybackSchedule.PlaybackEndAfterEventAtIndex(events, 0));
    }
}
