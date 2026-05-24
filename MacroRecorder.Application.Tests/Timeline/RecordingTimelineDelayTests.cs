using MacroRecorder.Application.Timeline;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class RecordingTimelineDelayTests
{
    [Fact]
    public void ComputeDelayBefore_returns_elapsed_minus_playback_end()
    {
        var delayBefore = RecordingTimelineDelay.ComputeDelayBefore(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(3), delayBefore);
    }

    [Fact]
    public void ComputeDelayBefore_clamps_negative_to_zero()
    {
        var delayBefore = RecordingTimelineDelay.ComputeDelayBefore(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.Zero, delayBefore);
    }
}
