using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.Application;

public sealed class RecordingCoordinator(IRecordingEngine engine)
{
    public bool IsRecording => engine.IsRunning;

    public void StartRecording(Action<RecordedInputEvent>? onEventRecorded = null, bool recordMouseMoves = true, int mouseMoveMinPixelDelta = 5) =>
        engine.Start(onEventRecorded, recordMouseMoves, mouseMoveMinPixelDelta);

    /// <summary>Stops the engine and returns this session's events (throws if not running).</summary>
    public RecordingEngineResult StopRecording() => engine.Stop();

    public Macro FinishRecording(string macroName)
    {
        var result = StopRecording();
        var metadata = RecordingMetadata.ForNewSession(result.Environment);
        return new Macro(MacroId.New(), macroName, metadata, result.Events, wasModifiedAfterRecording: false);
    }

    public void AbortRecording()
    {
        if (engine.IsRunning)
            _ = engine.Stop();
    }
}
