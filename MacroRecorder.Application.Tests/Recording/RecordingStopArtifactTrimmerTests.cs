using MacroRecorder.Application.Recording;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Recording;

public sealed class RecordingStopArtifactTrimmerTests
{
    private const string HostProcessName = "MacroRecorderByRezondes";

    [Fact]
    public void TrimTrailingHostStopArtifacts_removes_trailing_host_focus_and_click()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1),
            TestEvents.MouseLeftDown(2, 10, 10, TimeSpan.Zero),
            TestEvents.MouseLeftUp(3, 10, 10, TimeSpan.Zero),
            TestEvents.HostFocus(4, TimeSpan.Zero, HostProcessName)
        };

        RecordingStopArtifactTrimmer.TrimTrailingHostStopArtifacts(events, HostProcessName);

        Assert.Single(events);
    }

    [Fact]
    public void TrimTrailingHostStopArtifacts_does_not_remove_move_only_macro_entirely()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.MouseMove(1, 1, 1, TimeSpan.Zero),
            TestEvents.MouseMove(2, 2, 2, TimeSpan.Zero)
        };

        RecordingStopArtifactTrimmer.TrimTrailingHostStopArtifacts(events, HostProcessName);

        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void TrimTrailingHostStopArtifacts_removes_trailing_mouse_moves_when_other_events_remain()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1),
            TestEvents.MouseMove(2, 5, 5, TimeSpan.Zero),
            TestEvents.MouseMove(3, 6, 6, TimeSpan.Zero)
        };

        RecordingStopArtifactTrimmer.TrimTrailingHostStopArtifacts(events, HostProcessName);

        Assert.Single(events);
    }
}
