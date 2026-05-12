using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>
/// Counts editor-style timeline rows: consecutive <see cref="MouseMoveRecordedEvent"/> form one row.
/// Must stay aligned with the editor list (<c>EditorTimelineGrouper</c>) via <see cref="EnumerateActionRowGroups"/>.
/// </summary>
public static class TimelineActionRowCount
{
    public static int Count(IReadOnlyList<RecordedInputEvent> events) =>
        EnumerateActionRowGroups(events).Count();

    /// <summary>One group per editor row, in list order (not sorted by <see cref="RecordedInputEvent.Sequence"/>).</summary>
    public static IEnumerable<List<RecordedInputEvent>> EnumerateActionRowGroups(IReadOnlyList<RecordedInputEvent> events)
    {
        var ordered = events is List<RecordedInputEvent> list ? list : events.ToList();
        var flatIndex = 0;
        while (flatIndex < ordered.Count)
        {
            if (ordered[flatIndex] is MouseMoveRecordedEvent)
            {
                var group = new List<RecordedInputEvent>();
                while (flatIndex < ordered.Count && ordered[flatIndex] is MouseMoveRecordedEvent moveEvent)
                {
                    group.Add(moveEvent);
                    flatIndex++;
                }

                yield return group;
            }
            else
            {
                yield return new List<RecordedInputEvent> { ordered[flatIndex] };
                flatIndex++;
            }
        }
    }
}
