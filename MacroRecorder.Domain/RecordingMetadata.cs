namespace MacroRecorder.Domain;

public sealed record RecordingMetadata(
    int SchemaVersion,
    DateTimeOffset RecordedAtUtc,
    long StopwatchFrequency,
    RecordingEnvironment? Environment = null)
{
    public const int CurrentSchemaVersion = 1;

    public static RecordingMetadata ForNewSession(RecordingEnvironment? environment) =>
        new(
            CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            System.Diagnostics.Stopwatch.Frequency,
            environment);

    public static RecordingMetadata Empty() =>
        new(CurrentSchemaVersion, DateTimeOffset.MinValue, System.Diagnostics.Stopwatch.Frequency, null);
}
