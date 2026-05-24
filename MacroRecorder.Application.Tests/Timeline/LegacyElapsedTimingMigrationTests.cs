using MacroRecorder.Application.Timeline;
using MacroRecorder.Application.Tests.TestDoubles;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.Timeline;

public sealed class LegacyElapsedTimingMigrationTests
{
    [Fact]
    public void ApplyDelaysFromLegacyWaitUntilTimes_maps_legacy_targets_to_delay_before()
    {
        var events = new List<RecordedInputEvent>
        {
            TestEvents.KeyDown(sequence: 1),
            TestEvents.KeyDown(sequence: 2),
            TestEvents.SyntheticWait(3, TimeSpan.Zero, TimeSpan.FromMilliseconds(30))
        };
        var legacyTargets = new[]
        {
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(120),
            TimeSpan.FromMilliseconds(200)
        };

        LegacyElapsedTimingMigration.ApplyDelaysFromLegacyWaitUntilTimes(events, legacyTargets);

        Assert.Equal(TimeSpan.FromMilliseconds(50), events[0].DelayBefore);
        Assert.Equal(TimeSpan.FromMilliseconds(70), events[1].DelayBefore);
        Assert.Equal(TimeSpan.FromMilliseconds(80), events[2].DelayBefore);
    }

    [Fact]
    public void ApplyDelaysFromLegacyWaitUntilTimes_requires_matching_lengths()
    {
        var events = new List<RecordedInputEvent> { TestEvents.KeyDown() };

        Assert.Throws<ArgumentException>(() =>
            LegacyElapsedTimingMigration.ApplyDelaysFromLegacyWaitUntilTimes(events, []));
    }
}
