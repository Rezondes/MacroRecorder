using MacroRecorder.Application.Timeline;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class TimelineNormalizerTests
{
    [Fact]
    public void NormalizeInPlace_assigns_sequence_one_through_n()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 99),
            TestEvents.MouseMove(sequence: 5, screenX: 1, screenY: 2, delayBefore: TimeSpan.FromMilliseconds(5)),
            TestEvents.SyntheticWait(1, TimeSpan.Zero, TimeSpan.FromMilliseconds(10))
        };

        TimelineNormalizer.NormalizeInPlace(events);

        Assert.Equal(1UL, events[0].Sequence);
        Assert.Equal(2UL, events[1].Sequence);
        Assert.Equal(3UL, events[2].Sequence);
    }

    [Fact]
    public void NormalizeInPlace_assigns_sequence_for_all_event_types()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 9),
            TestEvents.KeyUp(sequence: 8),
            TestEvents.MouseMove(sequence: 7, screenX: 1, screenY: 2, delayBefore: TimeSpan.Zero),
            TestEvents.MouseLeftDown(6, 1, 1, TimeSpan.Zero),
            TestEvents.MouseLeftUp(5, 1, 1, TimeSpan.Zero),
            TestEvents.MouseWheel(4, wheelDelta: 120, delayBefore: TimeSpan.Zero),
            TestEvents.FocusLost(3, TimeSpan.Zero),
            TestEvents.SyntheticWait(2, TimeSpan.Zero, TimeSpan.FromMilliseconds(1))
        };

        TimelineNormalizer.NormalizeInPlace(events);

        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
            Assert.Equal((ulong)(eventIndex + 1), events[eventIndex].Sequence);
    }
}
