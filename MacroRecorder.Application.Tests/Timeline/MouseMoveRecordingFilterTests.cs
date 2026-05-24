using MacroRecorder.Application.Timeline;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class MouseMoveRecordingFilterTests
{
    [Fact]
    public void ShouldSkipMove_skips_collinear_point_when_no_button_is_down()
    {
        var shouldSkip = MouseMoveRecordingFilter.ShouldSkipMove(
            candidateX: 20,
            candidateY: 20,
            lastX: 10,
            lastY: 10,
            haveLastMove: true,
            secondLastX: 0,
            secondLastY: 0,
            haveSecondLastMove: true,
            minPixelDelta: 10,
            anyMouseButtonDown: false);

        Assert.True(shouldSkip);
    }

    [Fact]
    public void ShouldSkipMove_keeps_corner_point_on_free_path()
    {
        var shouldSkip = MouseMoveRecordingFilter.ShouldSkipMove(
            candidateX: 10,
            candidateY: 20,
            lastX: 10,
            lastY: 10,
            haveLastMove: true,
            secondLastX: 0,
            secondLastY: 0,
            haveSecondLastMove: true,
            minPixelDelta: 5,
            anyMouseButtonDown: false);

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipMove_does_not_apply_collinearity_while_button_is_down()
    {
        var shouldSkip = MouseMoveRecordingFilter.ShouldSkipMove(
            candidateX: 20,
            candidateY: 20,
            lastX: 10,
            lastY: 10,
            haveLastMove: true,
            secondLastX: 0,
            secondLastY: 0,
            haveSecondLastMove: true,
            minPixelDelta: 10,
            anyMouseButtonDown: true);

        Assert.False(shouldSkip);
    }
}
