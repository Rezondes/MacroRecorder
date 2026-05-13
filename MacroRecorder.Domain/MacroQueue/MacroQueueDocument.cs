using MacroRecorder.Domain;

namespace MacroRecorder.Domain.MacroQueue;

/// <summary>Persisted macro queue definition (JSON on disk).</summary>
public sealed record MacroQueueDocument(
    int SchemaVersion,
    string Name,
    IReadOnlyList<QueueStep> Steps,
    bool LoopWholeQueue = false)
{
    public const int CurrentSchemaVersion = 1;

    public static MacroQueueDocument Create(string name, IReadOnlyList<QueueStep> steps, bool loopWholeQueue = false) =>
        new(CurrentSchemaVersion, name, steps, loopWholeQueue);

    public MacroQueueDocument WithValidatedSteps() =>
        this with { Steps = Steps.Select(static step => step.WithValidatedCounts()).ToList() };
}
