using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class RecordingKeyboardHoldStateTests
{
    [Fact]
    public void IsAutorepeatKeyDown_false_for_first_key_down()
    {
        var holdState = new RecordingKeyboardHoldState();

        Assert.False(holdState.IsAutorepeatKeyDown(0x41));
    }

    [Fact]
    public void IsAutorepeatKeyDown_true_after_same_vk_stored()
    {
        var holdState = new RecordingKeyboardHoldState();
        holdState.OnKeyDownStored(0x41);

        Assert.True(holdState.IsAutorepeatKeyDown(0x41));
    }

    [Fact]
    public void KeyUp_clears_autorepeat_for_vk()
    {
        var holdState = new RecordingKeyboardHoldState();
        holdState.OnKeyDownStored(0x41);
        holdState.OnKeyUpStored(0x41);

        Assert.False(holdState.IsAutorepeatKeyDown(0x41));
    }

    [Fact]
    public void Reset_clears_all_held_keys()
    {
        var holdState = new RecordingKeyboardHoldState();
        holdState.OnKeyDownStored(0x41);
        holdState.Reset();

        Assert.False(holdState.IsAutorepeatKeyDown(0x41));
    }

    [Fact]
    public void HoldState_matches_legacy_backward_scan_for_mixed_timelines()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(1, TimeSpan.Zero, 0x41),
            TestEvents.KeyUp(2, TimeSpan.Zero),
            TestEvents.KeyDown(3, TimeSpan.Zero, 0x42),
            new SyntheticWaitRecordedEvent
            {
                DelayBefore = TimeSpan.FromMilliseconds(50),
                Sequence = 4,
                AdditionalDelay = TimeSpan.FromMilliseconds(100)
            },
            TestEvents.KeyDown(5, TimeSpan.Zero, 0x42)
        };

        var holdState = new RecordingKeyboardHoldState();
        var stored = new List<RecordedInputEvent>();
        foreach (var recordedEvent in events)
        {
            if (recordedEvent is KeyDownRecordedEvent keyDown)
            {
                var legacy = LegacyIsKeyboardAutorepeatKeyDown(stored, keyDown.Vk);
                var optimized = holdState.IsAutorepeatKeyDown(keyDown.Vk);
                Assert.Equal(legacy, optimized);
                holdState.OnKeyDownStored(keyDown.Vk);
                stored.Add(keyDown);
            }
            else if (recordedEvent is KeyUpRecordedEvent keyUp)
            {
                holdState.OnKeyUpStored(keyUp.Vk);
                stored.Add(keyUp);
            }
            else
            {
                stored.Add(recordedEvent);
            }
        }
    }

    /// <summary>Reference implementation of the pre-O(1) engine scan.</summary>
    private static bool LegacyIsKeyboardAutorepeatKeyDown(
        IReadOnlyList<RecordedInputEvent> events,
        ushort virtualKey)
    {
        for (var eventIndex = events.Count - 1; eventIndex >= 0; eventIndex--)
        {
            switch (events[eventIndex])
            {
                case SyntheticWaitRecordedEvent:
                    continue;
                case KeyDownRecordedEvent previousKeyDown:
                    return previousKeyDown.Vk == virtualKey;
                default:
                    return false;
            }
        }

        return false;
    }
}
