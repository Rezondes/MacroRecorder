using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Editor;

public static class EditorTimelineGrouper
{
    /// <summary>
    /// Builds rows in the same order as <paramref name="events"/> (playback / editor list order).
    /// Do not sort by <see cref="RecordedInputEvent.Sequence"/> here: appended recordings reuse low
    /// sequence numbers until <see cref="MacroRecorder.Application.Timeline.TimelineNormalizer"/> runs.
    /// </summary>
    public static IReadOnlyList<EditorTimelineRow> BuildRows(IReadOnlyList<RecordedInputEvent> events, IUiLocalizer loc)
    {
        var ordered = events is List<RecordedInputEvent> list ? list : events.ToList();
        var rows = new List<EditorTimelineRow>();
        var flatIndex = 0;
        while (flatIndex < ordered.Count)
        {
            if (ordered[flatIndex] is MouseMoveRecordedEvent)
            {
                var group = new List<MouseMoveRecordedEvent>();
                while (flatIndex < ordered.Count && ordered[flatIndex] is MouseMoveRecordedEvent moveEvent)
                {
                    group.Add(moveEvent);
                    flatIndex++;
                }

                rows.Add(new EditorMousePathRow(group, loc));
            }
            else
            {
                rows.Add(new EditorSingleEventRow(ordered[flatIndex], loc));
                flatIndex++;
            }
        }

        return rows;
    }
}
