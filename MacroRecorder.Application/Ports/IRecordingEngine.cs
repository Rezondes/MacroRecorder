using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IRecordingEngine : IDisposable
{
    bool IsRunning { get; }

    /// <param name="onEventRecorded">Optional: invoked on the hook thread after each event is stored; marshal to UI if needed.</param>
    void Start(Action<RecordedInputEvent>? onEventRecorded = null);

    RecordingEngineResult Stop();
}

public sealed record RecordingEngineResult(
    IReadOnlyList<RecordedInputEvent> Events,
    RecordingEnvironment Environment);
