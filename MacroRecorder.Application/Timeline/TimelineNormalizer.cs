using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>Reassigns <see cref="RecordedInputEvent.Sequence"/> to 1..n in list order after edits or merges.</summary>
public static class TimelineNormalizer
{
    public static void NormalizeInPlace(List<RecordedInputEvent> playbackOrder)
    {
        for (var eventIndex = 0; eventIndex < playbackOrder.Count; eventIndex++)
        {
            var sequence = (ulong)(eventIndex + 1);
            playbackOrder[eventIndex] = WithSequence(playbackOrder[eventIndex], sequence);
        }
    }

    private static RecordedInputEvent WithSequence(RecordedInputEvent recordedEvent, ulong sequence) =>
        recordedEvent switch
        {
            KeyDownRecordedEvent keyDown => keyDown with { Sequence = sequence },
            KeyUpRecordedEvent keyUp => keyUp with { Sequence = sequence },
            MouseMoveRecordedEvent mouseMove => mouseMove with { Sequence = sequence },
            MouseButtonDownRecordedEvent mouseButtonDown => mouseButtonDown with { Sequence = sequence },
            MouseButtonUpRecordedEvent mouseButtonUp => mouseButtonUp with { Sequence = sequence },
            MouseWheelRecordedEvent mouseWheel => mouseWheel with { Sequence = sequence },
            FocusChangedRecordedEvent focusChanged => focusChanged with { Sequence = sequence },
            SyntheticWaitRecordedEvent syntheticWait => syntheticWait with { Sequence = sequence },
            _ => throw new InvalidOperationException($"Unsupported event type: {recordedEvent.GetType().Name}")
        };
}
