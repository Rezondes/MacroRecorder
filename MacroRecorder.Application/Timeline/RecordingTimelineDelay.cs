namespace MacroRecorder.Application.Timeline;

/// <summary>Maps stopwatch elapsed time to per-event <see cref="Domain.RecordedInputEvent.DelayBefore"/> during recording.</summary>
public static class RecordingTimelineDelay
{
    /// <summary>Idle time since the last stored event finished on the playback timeline.</summary>
    public static TimeSpan ComputeDelayBefore(TimeSpan elapsed, TimeSpan playbackTimelineEnd)
    {
        var delayBefore = elapsed - playbackTimelineEnd;
        return delayBefore < TimeSpan.Zero ? TimeSpan.Zero : delayBefore;
    }
}
