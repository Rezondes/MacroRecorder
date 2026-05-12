using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
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
        foreach (var group in TimelineActionRowCount.EnumerateActionRowGroups(ordered))
        {
            if (group[0] is MouseMoveRecordedEvent)
            {
                var moves = group.Cast<MouseMoveRecordedEvent>().ToList();
                rows.Add(new EditorMousePathRow(moves, loc));
            }
            else
                rows.Add(new EditorSingleEventRow(group[0], loc));
        }

        return rows;
    }
}
