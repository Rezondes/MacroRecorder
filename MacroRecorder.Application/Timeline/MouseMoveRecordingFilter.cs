namespace MacroRecorder.Application.Timeline;

/// <summary>Online mouse-move filtering during recording (min distance + collinearity while no button is held).</summary>
public static class MouseMoveRecordingFilter
{
    /// <summary>Returns true when the candidate move can be dropped without affecting stored path fidelity.</summary>
    public static bool ShouldSkipMove(
        int candidateX,
        int candidateY,
        int lastX,
        int lastY,
        bool haveLastMove,
        int secondLastX,
        int secondLastY,
        bool haveSecondLastMove,
        int minPixelDelta,
        bool anyMouseButtonDown)
    {
        if (haveLastMove && candidateX == lastX && candidateY == lastY)
            return true;

        if (haveLastMove)
        {
            var deltaX = candidateX - lastX;
            var deltaY = candidateY - lastY;
            var minDistanceSq = (long)minPixelDelta * minPixelDelta;
            if ((long)deltaX * deltaX + (long)deltaY * deltaY < minDistanceSq)
                return true;
        }

        if (!anyMouseButtonDown && haveLastMove && haveSecondLastMove)
        {
            var epsilonSq = (long)minPixelDelta * minPixelDelta;
            if (IsNearLineSegmentSq(candidateX, candidateY, secondLastX, secondLastY, lastX, lastY, epsilonSq))
                return true;
        }

        return false;
    }

    /// <summary>Squared perpendicular distance from point P to segment A→B, compared against <paramref name="epsilonSq"/> via cross product.</summary>
    internal static bool IsNearLineSegmentSq(int pointX, int pointY, int ax, int ay, int bx, int by, long epsilonSq)
    {
        var segmentDeltaX = bx - ax;
        var segmentDeltaY = by - ay;
        var segmentLengthSq = (long)segmentDeltaX * segmentDeltaX + (long)segmentDeltaY * segmentDeltaY;
        if (segmentLengthSq == 0)
            return false;

        var cross = (long)(pointX - ax) * segmentDeltaY - (long)(pointY - ay) * segmentDeltaX;
        return cross * cross <= epsilonSq * segmentLengthSq;
    }
}
