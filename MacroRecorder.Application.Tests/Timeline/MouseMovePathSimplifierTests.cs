using MacroRecorder.Application.Timeline;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class MouseMovePathSimplifierTests
{
    [Fact]
    public void SimplifyInPlace_collapses_straight_line_to_endpoints()
    {
        var events = new List<RecordedInputEvent>();
        for (var step = 0; step < 20; step++)
            events.Add(TestEvents.MouseMove((ulong)(step + 1), step * 10, 0, TimeSpan.Zero));

        var beforeTargets = EventPlaybackSchedule.ComputeWaitUntilTargets(events);
        MouseMovePathSimplifier.SimplifyInPlace(events, epsilonPixels: 5);

        Assert.Equal(2, events.Count);
        Assert.Equal(0, ((MouseMoveRecordedEvent)events[0]).ScreenX);
        Assert.Equal(190, ((MouseMoveRecordedEvent)events[1]).ScreenX);
        Assert.Equal(beforeTargets[^1], EventPlaybackSchedule.ComputeWaitUntilTargets(events)[^1]);
    }

    [Fact]
    public void SimplifyInPlace_keeps_corner_on_right_angle_path()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.MouseMove(1, 0, 0, TimeSpan.Zero),
            TestEvents.MouseMove(2, 10, 0, TimeSpan.Zero),
            TestEvents.MouseMove(3, 20, 0, TimeSpan.Zero),
            TestEvents.MouseMove(4, 30, 0, TimeSpan.Zero),
            TestEvents.MouseMove(5, 30, 10, TimeSpan.Zero),
            TestEvents.MouseMove(6, 30, 20, TimeSpan.Zero),
            TestEvents.MouseMove(7, 30, 30, TimeSpan.Zero)
        };

        MouseMovePathSimplifier.SimplifyInPlace(events, epsilonPixels: 5);

        Assert.Equal(3, events.Count);
        Assert.Equal(0, ((MouseMoveRecordedEvent)events[0]).ScreenX);
        Assert.Equal(30, ((MouseMoveRecordedEvent)events[1]).ScreenX);
        Assert.Equal(0, ((MouseMoveRecordedEvent)events[1]).ScreenY);
        Assert.Equal(30, ((MouseMoveRecordedEvent)events[2]).ScreenX);
        Assert.Equal(30, ((MouseMoveRecordedEvent)events[2]).ScreenY);
    }

    [Fact]
    public void SimplifyInPlace_leaves_drag_segment_untouched()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.MouseLeftDown(1, 0, 0, TimeSpan.Zero),
            TestEvents.MouseMove(2, 10, 0, TimeSpan.Zero),
            TestEvents.MouseMove(3, 20, 0, TimeSpan.Zero),
            TestEvents.MouseMove(4, 30, 0, TimeSpan.Zero),
            TestEvents.MouseLeftUp(5, 30, 0, TimeSpan.Zero)
        };

        MouseMovePathSimplifier.SimplifyInPlace(events, epsilonPixels: 50);

        Assert.Equal(5, events.Count);
        Assert.IsType<MouseMoveRecordedEvent>(events[1]);
        Assert.IsType<MouseMoveRecordedEvent>(events[2]);
        Assert.IsType<MouseMoveRecordedEvent>(events[3]);
    }

    [Fact]
    public void SimplifyInPlace_preserves_total_playback_duration_for_timed_move_path()
    {
        var events = new List<RecordedInputEvent>();
        for (var step = 0; step < 20; step++)
            events.Add(TestEvents.MouseMove((ulong)(step + 1), step * 10, 0, TimeSpan.FromMilliseconds(250)));

        var beforeDuration = PlaybackDurationEstimator.EstimateTotalPlaybackDuration(events);
        MouseMovePathSimplifier.SimplifyInPlace(events, epsilonPixels: 5);
        var afterDuration = PlaybackDurationEstimator.EstimateTotalPlaybackDuration(events);

        Assert.Equal(2, events.Count);
        Assert.Equal(beforeDuration, afterDuration);
    }

    [Fact]
    public void SimplifyInPlace_preserves_wait_until_schedule_with_non_zero_delays()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.MouseMove(1, 0, 0, TimeSpan.Zero),
            TestEvents.MouseMove(2, 10, 0, TimeSpan.FromMilliseconds(5)),
            TestEvents.MouseMove(3, 20, 0, TimeSpan.FromMilliseconds(10)),
            TestEvents.MouseMove(4, 30, 0, TimeSpan.FromMilliseconds(15))
        };

        var beforeTargets = EventPlaybackSchedule.ComputeWaitUntilTargets(events);
        MouseMovePathSimplifier.SimplifyInPlace(events, epsilonPixels: 5);
        var afterTargets = EventPlaybackSchedule.ComputeWaitUntilTargets(events);

        Assert.Equal(beforeTargets[0], afterTargets[0]);
        Assert.Equal(beforeTargets[^1], afterTargets[^1]);
    }

    [Fact]
    public void SimplifyInPlace_is_idempotent()
    {
        var events = new List<RecordedInputEvent>();
        for (var step = 0; step < 15; step++)
            events.Add(TestEvents.MouseMove((ulong)(step + 1), step * 8, step * 4, TimeSpan.Zero));
        events.Add(TestEvents.MouseLeftDown(16, 120, 60, TimeSpan.Zero));

        MouseMovePathSimplifier.SimplifyInPlace(events, epsilonPixels: 6);
        var firstPass = events.Select(CloneEvent).ToList();
        MouseMovePathSimplifier.SimplifyInPlace(events, epsilonPixels: 6);

        Assert.Equal(firstPass.Count, events.Count);
        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            var left = firstPass[eventIndex];
            var right = events[eventIndex];
            Assert.Equal(left.GetType(), right.GetType());
            Assert.Equal(left.Sequence, right.Sequence);
            if (left is MouseMoveRecordedEvent leftMove && right is MouseMoveRecordedEvent rightMove)
            {
                Assert.Equal(leftMove.ScreenX, rightMove.ScreenX);
                Assert.Equal(leftMove.ScreenY, rightMove.ScreenY);
            }
        }
    }

    private static RecordedInputEvent CloneEvent(RecordedInputEvent recordedEvent) =>
        recordedEvent switch
        {
            MouseMoveRecordedEvent mouseMove => mouseMove with { },
            MouseButtonDownRecordedEvent mouseButtonDown => mouseButtonDown with { },
            _ => recordedEvent
        };
}
