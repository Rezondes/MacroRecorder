using MacroRecorder.Application.Timeline;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class PlaybackDurationEstimatorTests
{
    [Fact]
    public void EstimateTotalPlaybackDuration_returns_zero_for_empty_list()
    {
        var duration = PlaybackDurationEstimator.EstimateTotalPlaybackDuration([]);

        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void EstimateTotalPlaybackDuration_sums_normalized_timeline()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1, delayBefore: TimeSpan.FromMilliseconds(100)),
            TestEvents.SyntheticWait(2, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(50)),
            TestEvents.KeyDown(sequence: 3, delayBefore: TimeSpan.FromMilliseconds(25))
        };

        var duration = PlaybackDurationEstimator.EstimateTotalPlaybackDuration(events);

        Assert.Equal(TimeSpan.FromMilliseconds(175), duration);
    }
}
