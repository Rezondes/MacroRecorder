using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IRecordingEngine : IDisposable
{
    bool IsRunning { get; }

    /// <param name="onEventRecorded">Optional: invoked on the hook thread after each event is stored; marshal to UI if needed.</param>
    /// <param name="recordMouseMoves">When false, mouse-move messages are ignored and gaps between key/button events become <see cref="SyntheticWaitRecordedEvent"/>.</param>
    /// <param name="mouseMoveMinPixelDelta">Minimum Euclidean distance in pixels between stored mouse moves when <paramref name="recordMouseMoves"/> is true (clamped by the engine).</param>
    /// <param name="useFocusBoundMouseCoordinates">When true, mouse coordinates are global (screen) until a <see cref="FocusChangedRecordedEvent"/> with a window;
    /// then client-space relative to the foreground window until focus is lost or another focus row.</param>
    void Start(
        Action<RecordedInputEvent>? onEventRecorded = null,
        bool recordMouseMoves = true,
        int mouseMoveMinPixelDelta = 5,
        bool useFocusBoundMouseCoordinates = false);

    RecordingEngineResult Stop();
}

public sealed record RecordingEngineResult(
    IReadOnlyList<RecordedInputEvent> Events,
    RecordingEnvironment Environment,
    bool UseFocusBoundMouseCoordinates);
