using System.Text.Json.Serialization;

namespace MacroRecorder.Domain;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "discriminator")]
[JsonDerivedType(typeof(KeyDownRecordedEvent), "keyDown")]
[JsonDerivedType(typeof(KeyUpRecordedEvent), "keyUp")]
[JsonDerivedType(typeof(MouseMoveRecordedEvent), "mouseMove")]
[JsonDerivedType(typeof(MouseButtonDownRecordedEvent), "mouseButtonDown")]
[JsonDerivedType(typeof(MouseButtonUpRecordedEvent), "mouseButtonUp")]
[JsonDerivedType(typeof(MouseWheelRecordedEvent), "mouseWheel")]
[JsonDerivedType(typeof(FocusChangedRecordedEvent), "focusChanged")]
[JsonDerivedType(typeof(SyntheticWaitRecordedEvent), "syntheticWait")]
public abstract record RecordedInputEvent
{
    /// <summary>Idle time after the previous event finishes before this step runs (<c>WaitUntil</c> advance only).</summary>
    public required TimeSpan DelayBefore { get; init; }
    public required ulong Sequence { get; init; }
}

public sealed record KeyDownRecordedEvent : RecordedInputEvent
{
    public required ushort Vk { get; init; }
    public required ushort ScanCode { get; init; }
    public required bool IsExtendedKey { get; init; }
    public required bool IsAltDown { get; init; }
    public required bool IsInjected { get; init; }
    public int RepeatCount { get; init; }
}

public sealed record KeyUpRecordedEvent : RecordedInputEvent
{
    public required ushort Vk { get; init; }
    public required ushort ScanCode { get; init; }
    public required bool IsExtendedKey { get; init; }
    public required bool IsAltDown { get; init; }
    public required bool IsInjected { get; init; }
}

public sealed record MouseMoveRecordedEvent : RecordedInputEvent
{
    public required int ScreenX { get; init; }
    public required int ScreenY { get; init; }
}

public sealed record MouseButtonDownRecordedEvent : RecordedInputEvent
{
    public required MouseButtonKind Button { get; init; }
    public required int ScreenX { get; init; }
    public required int ScreenY { get; init; }
}

public sealed record MouseButtonUpRecordedEvent : RecordedInputEvent
{
    public required MouseButtonKind Button { get; init; }
    public required int ScreenX { get; init; }
    public required int ScreenY { get; init; }
}

public sealed record MouseWheelRecordedEvent : RecordedInputEvent
{
    public required int ScreenX { get; init; }
    public required int ScreenY { get; init; }
    public required int WheelDelta { get; init; }
    public required bool IsHorizontal { get; init; }
}

public sealed record FocusChangedRecordedEvent : RecordedInputEvent
{
    public ulong? Hwnd { get; init; }
    public required string WindowTitle { get; init; }
    public required string ProcessName { get; init; }
    public uint? ProcessId { get; init; }
}

public sealed record SyntheticWaitRecordedEvent : RecordedInputEvent
{
    public required TimeSpan AdditionalDelay { get; init; }
}
