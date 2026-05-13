using MacroRecorder.Domain;

namespace MacroRecorder.Domain.MacroQueue;

/// <summary>One queue entry: play a macro one or more times with optional delays.</summary>
public sealed record QueueStep(
    MacroId MacroId,
    int RepeatCount = 1,
    TimeSpan InitialDelay = default,
    TimeSpan DelayBetweenRuns = default,
    TimeSpan PostStepDelay = default)
{
    public QueueStep WithValidatedCounts()
    {
        var repeats = RepeatCount < 1 ? 1 : RepeatCount;
        return repeats == RepeatCount ? this : this with { RepeatCount = repeats };
    }
}
