using System.Diagnostics;
using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Recording;
using MacroRecorder.Domain;

namespace MacroRecorder.Application;

public sealed class RecordingCoordinator(IRecordingEngine engine)
{
    public bool IsRecording => engine.IsRunning;

    public void StartRecording(
        Action<RecordedInputEvent>? onEventRecorded = null,
        bool recordMouseMoves = true,
        int mouseMoveMinPixelDelta = 5,
        bool useFocusBoundMouseCoordinates = false) =>
        engine.Start(onEventRecorded, recordMouseMoves, mouseMoveMinPixelDelta, useFocusBoundMouseCoordinates);

    /// <summary>Stops the engine and returns this session's events (throws if not running).</summary>
    public RecordingEngineResult StopRecording() => engine.Stop();

    public Macro FinishRecording(string macroName)
    {
        var result = StopRecording();
        var events = result.Events.ToList();
        RecordingStopArtifactTrimmer.TrimTrailingHostStopArtifacts(events, Process.GetCurrentProcess().ProcessName);
        var metadata = RecordingMetadata.ForNewSession(result.Environment) with
        {
            UseFocusBoundMouseCoordinates = result.UseFocusBoundMouseCoordinates,
            MouseAnchor = null
        };
        return new Macro(MacroId.New(), macroName, metadata, events, wasModifiedAfterRecording: false);
    }

    public void AbortRecording()
    {
        if (engine.IsRunning)
            _ = engine.Stop();
    }
}
