using MacroRecorder.Domain;

namespace MacroRecorder.Application.Timeline;

/// <summary>Reduces contiguous mouse-move paths with Ramer–Douglas–Peucker while preserving playback schedule and drag segments.</summary>
public static class MouseMovePathSimplifier
{
    public static void SimplifyInPlace(List<RecordedInputEvent> events, int epsilonPixels)
    {
        if (events.Count == 0)
            return;

        var epsilon = Math.Clamp(epsilonPixels, 1, 10_000);
        var dragMoveIndices = ComputeDragMoveIndices(events);
        var simplified = new List<RecordedInputEvent>(events.Count);
        var flatIndex = 0;

        foreach (var group in TimelineActionRowCount.EnumerateActionRowGroups(events))
        {
            if (group[0] is not MouseMoveRecordedEvent)
            {
                simplified.Add(group[0]);
                flatIndex++;
                continue;
            }

            var moves = group.Cast<MouseMoveRecordedEvent>().ToList();
            var isDragPath = dragMoveIndices[flatIndex];
            flatIndex += group.Count;

            if (isDragPath || moves.Count <= 2)
            {
                simplified.AddRange(moves);
                continue;
            }

            simplified.AddRange(SimplifyFreeMovePath(moves, epsilon));
        }

        events.Clear();
        events.AddRange(simplified);
        TimelineNormalizer.NormalizeInPlace(events);
    }

    private static bool[] ComputeDragMoveIndices(IReadOnlyList<RecordedInputEvent> events)
    {
        var inDragMove = new bool[events.Count];
        var leftButtonDown = false;
        var rightButtonDown = false;
        var middleButtonDown = false;

        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            switch (events[eventIndex])
            {
                case MouseMoveRecordedEvent:
                    inDragMove[eventIndex] = leftButtonDown || rightButtonDown || middleButtonDown;
                    break;
                case MouseButtonDownRecordedEvent mouseButtonDown:
                    SetButtonDown(mouseButtonDown.Button, true, ref leftButtonDown, ref rightButtonDown, ref middleButtonDown);
                    break;
                case MouseButtonUpRecordedEvent mouseButtonUp:
                    SetButtonDown(mouseButtonUp.Button, false, ref leftButtonDown, ref rightButtonDown, ref middleButtonDown);
                    break;
            }
        }

        return inDragMove;
    }

    private static void SetButtonDown(
        MouseButtonKind button,
        bool isDown,
        ref bool leftButtonDown,
        ref bool rightButtonDown,
        ref bool middleButtonDown)
    {
        switch (button)
        {
            case MouseButtonKind.Left:
                leftButtonDown = isDown;
                break;
            case MouseButtonKind.Right:
                rightButtonDown = isDown;
                break;
            case MouseButtonKind.Middle:
                middleButtonDown = isDown;
                break;
        }
    }

    private static List<MouseMoveRecordedEvent> SimplifyFreeMovePath(IReadOnlyList<MouseMoveRecordedEvent> moves, int epsilon)
    {
        if (moves.Count <= 2)
            return moves.ToList();

        var keep = new bool[moves.Count];
        keep[0] = true;
        keep[^1] = true;
        MarkRdpKeepers(moves, 0, moves.Count - 1, epsilon, keep);

        var simplified = new List<MouseMoveRecordedEvent>();
        var pendingDelay = TimeSpan.Zero;
        for (var moveIndex = 0; moveIndex < moves.Count; moveIndex++)
        {
            pendingDelay += moves[moveIndex].DelayBefore;
            if (!keep[moveIndex])
                continue;

            simplified.Add(moves[moveIndex] with { DelayBefore = pendingDelay });
            pendingDelay = TimeSpan.Zero;
        }

        return simplified;
    }

    private static void MarkRdpKeepers(
        IReadOnlyList<MouseMoveRecordedEvent> moves,
        int startIndex,
        int endIndex,
        int epsilon,
        bool[] keep)
    {
        if (endIndex <= startIndex + 1)
            return;

        var epsilonSq = (long)epsilon * epsilon;
        var maxDistanceSq = -1L;
        var farthestIndex = startIndex;

        var start = moves[startIndex];
        var end = moves[endIndex];
        for (var moveIndex = startIndex + 1; moveIndex < endIndex; moveIndex++)
        {
            var candidate = moves[moveIndex];
            var distanceSq = PerpendicularDistanceSq(
                candidate.ScreenX,
                candidate.ScreenY,
                start.ScreenX,
                start.ScreenY,
                end.ScreenX,
                end.ScreenY);
            if (distanceSq > maxDistanceSq)
            {
                maxDistanceSq = distanceSq;
                farthestIndex = moveIndex;
            }
        }

        if (maxDistanceSq <= epsilonSq)
            return;

        keep[farthestIndex] = true;
        MarkRdpKeepers(moves, startIndex, farthestIndex, epsilon, keep);
        MarkRdpKeepers(moves, farthestIndex, endIndex, epsilon, keep);
    }

    private static long PerpendicularDistanceSq(int pointX, int pointY, int ax, int ay, int bx, int by)
    {
        var segmentDeltaX = bx - ax;
        var segmentDeltaY = by - ay;
        var segmentLengthSq = (long)segmentDeltaX * segmentDeltaX + (long)segmentDeltaY * segmentDeltaY;
        if (segmentLengthSq == 0)
        {
            var deltaX = pointX - ax;
            var deltaY = pointY - ay;
            return (long)deltaX * deltaX + (long)deltaY * deltaY;
        }

        var cross = (long)(pointX - ax) * segmentDeltaY - (long)(pointY - ay) * segmentDeltaX;
        return cross * cross / segmentLengthSq;
    }
}
