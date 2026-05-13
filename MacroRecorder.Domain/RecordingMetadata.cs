namespace MacroRecorder.Domain;

public sealed record RecordingMetadata(
    int SchemaVersion,
    DateTimeOffset RecordedAtUtc,
    long StopwatchFrequency,
    RecordingEnvironment? Environment = null,
    bool UseFocusBoundMouseCoordinates = false,
    MousePlaybackAnchor? MouseAnchor = null)
{
    public const int CurrentSchemaVersion = 2;

    public static RecordingMetadata ForNewSession(RecordingEnvironment? environment) =>
        new(
            CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            System.Diagnostics.Stopwatch.Frequency,
            environment,
            UseFocusBoundMouseCoordinates: false,
            MouseAnchor: null);

    public static RecordingMetadata Empty() =>
        new(CurrentSchemaVersion, DateTimeOffset.MinValue, System.Diagnostics.Stopwatch.Frequency, null, false, null);
}
