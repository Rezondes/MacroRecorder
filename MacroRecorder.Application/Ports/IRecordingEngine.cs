using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IRecordingEngine : IDisposable
{
    bool IsRunning { get; }

    /// <param name="onEventRecorded">Optional: invoked on the hook thread after each event is stored; marshal to UI if needed.</param>
    /// <param name="recordMouseMoves">When false, mouse-move messages are ignored and gaps between key/button events become <see cref="SyntheticWaitRecordedEvent"/>.</param>
    void Start(Action<RecordedInputEvent>? onEventRecorded = null, bool recordMouseMoves = true);

    RecordingEngineResult Stop();
}

public sealed record RecordingEngineResult(
    IReadOnlyList<RecordedInputEvent> Events,
    RecordingEnvironment Environment);
