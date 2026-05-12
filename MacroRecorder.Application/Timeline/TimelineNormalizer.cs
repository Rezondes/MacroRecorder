using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// Rebuilds Sequence and ElapsedSinceSessionStart for a playback-ordered list
/// so playback receives a coherent timeline.
public static class TimelineNormalizer
{
    public static void NormalizeInPlace(List<RecordedInputEvent> playbackOrder)
    {
        if (playbackOrder.Count == 0)
            return;

        var oldElapsed = playbackOrder.Select(e => e.ElapsedSinceSessionStart).ToList();
        var rebuilt = new List<RecordedInputEvent>(playbackOrder.Count);
        var cursor = TimeSpan.Zero;

        for (var eventIndex = 0; eventIndex < playbackOrder.Count; eventIndex++)
        {
            if (eventIndex > 0)
            {
                var delta = oldElapsed[eventIndex] - oldElapsed[eventIndex - 1];
                if (delta < TimeSpan.Zero)
                    delta = TimeSpan.Zero;
                cursor += delta;
                if (playbackOrder[eventIndex - 1] is SyntheticWaitRecordedEvent syntheticWait)
                    cursor += syntheticWait.AdditionalDelay;
            }

            var sequence = (ulong)(eventIndex + 1);
            rebuilt.Add(WithTiming(playbackOrder[eventIndex], cursor, sequence));
        }

        playbackOrder.Clear();
        playbackOrder.AddRange(rebuilt);
    }

    private static RecordedInputEvent WithTiming(RecordedInputEvent recordedEvent, TimeSpan elapsed, ulong sequence) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            KeyUpRecordedEvent keyUp => keyUp with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseMoveRecordedEvent mouseMove =>
                mouseMove with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseButtonDownRecordedEvent mouseButtonDown =>
                mouseButtonDown with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseButtonUpRecordedEvent mouseButtonUp =>
                mouseButtonUp with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            MouseWheelRecordedEvent mouseWheel =>
                mouseWheel with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            FocusChangedRecordedEvent focusChanged =>
                focusChanged with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            SyntheticWaitRecordedEvent syntheticWait =>
                syntheticWait with { ElapsedSinceSessionStart = elapsed, Sequence = sequence },
            _ => throw new InvalidOperationException($"Unsupported event type: {recordedEvent.GetType().Name}")
        };
}
