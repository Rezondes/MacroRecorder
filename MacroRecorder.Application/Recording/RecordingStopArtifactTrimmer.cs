using MacroRecorder.Domain;

namespace MacroRecorder.Application.Recording;

/// <summary>
/// Removes trailing events caused by stopping recording: focus returns to Macro Recorder, the stop-button click,
/// and (often) a long run of <see cref="MouseMoveRecordedEvent"/> as the cursor moves back to the app.
/// </summary>
/// <remarks>
/// When appending a session to an existing timeline, trim <b>only the new session list</b> before concatenating.
/// Otherwise a prior trailing mouse-move path merges with the stop tail into one move-only suffix and the
/// move-only guard (<see cref="TryRemoveTrailingMouseMoveSuffix"/>) skips removal.
/// </remarks>
public static class RecordingStopArtifactTrimmer
{
    /// <summary>
    /// Mutates <paramref name="events"/> in place (typically one recording session). Safe for empty lists.</summary>
    public static void TrimTrailingHostStopArtifacts(List<RecordedInputEvent> events, string hostProcessName)
    {
        if (events.Count == 0)
            return;

        var comparer = StringComparer.OrdinalIgnoreCase;

        while (true)
        {
            var progressed = false;

            if (!string.IsNullOrWhiteSpace(hostProcessName))
            {
                while (events.Count > 0 &&
                       events[^1] is FocusChangedRecordedEvent focus &&
                       comparer.Equals(focus.ProcessName, hostProcessName))
                {
                    events.RemoveAt(events.Count - 1);
                    progressed = true;
                }

                if (progressed)
                    TryRemoveTrailingLeftClickWithOptionalFillers(events);
            }

            if (TryRemoveTrailingMouseMoveSuffix(events))
                progressed = true;

            if (!progressed)
                break;
        }
    }

    /// <summary>Removes a contiguous suffix of only mouse-move events. Skips when that would delete the entire
    /// timeline (macros that are move-only).</summary>
    private static bool TryRemoveTrailingMouseMoveSuffix(List<RecordedInputEvent> events)
    {
        if (events.Count == 0)
            return false;
        var lastIndex = events.Count - 1;
        if (events[lastIndex] is not MouseMoveRecordedEvent)
            return false;

        var index = lastIndex;
        while (index >= 0 && events[index] is MouseMoveRecordedEvent)
            index--;

        var firstMoveIndex = index + 1;
        if (firstMoveIndex == 0)
            return false;

        if (firstMoveIndex > lastIndex)
            return false;

        events.RemoveRange(firstMoveIndex, events.Count - firstMoveIndex);
        return true;
    }

    /// <summary>Walks backward from a left <see cref="MouseButtonUpRecordedEvent"/> to a matching left
    /// <see cref="MouseButtonDownRecordedEvent"/>, allowing only synthetic waits and focus-lost rows in between.</summary>
    private static void TryRemoveTrailingLeftClickWithOptionalFillers(List<RecordedInputEvent> events)
    {
        var lastIndex = events.Count - 1;
        if (lastIndex < 0)
            return;
        if (events[lastIndex] is not MouseButtonUpRecordedEvent up || up.Button != MouseButtonKind.Left)
            return;

        var x = up.ScreenX;
        var y = up.ScreenY;
        var index = lastIndex - 1;
        while (index >= 0 && IsAllowedBetweenStopClickDownAndUp(events[index]))
            index--;

        if (index < 0 ||
            events[index] is not MouseButtonDownRecordedEvent down ||
            down.Button != MouseButtonKind.Left ||
            down.ScreenX != x ||
            down.ScreenY != y)
            return;

        events.RemoveRange(index, events.Count - index);
    }

    private static bool IsAllowedBetweenStopClickDownAndUp(RecordedInputEvent recordedEvent) =>
        recordedEvent is SyntheticWaitRecordedEvent ||
        recordedEvent is FocusChangedRecordedEvent { Hwnd: null };
}
