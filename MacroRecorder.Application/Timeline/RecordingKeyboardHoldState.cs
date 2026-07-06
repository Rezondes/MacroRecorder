namespace MacroRecorder.Application.Timeline;

/// <summary>
/// O(1) keyboard hold tracking for recording autorepeat suppression.
/// Mirrors scanning <c>_events</c> backward skipping <see cref="Domain.SyntheticWaitRecordedEvent"/>.
/// </summary>
public sealed class RecordingKeyboardHoldState
{
    private readonly HashSet<ushort> _keysDown = new();

    public void Reset() => _keysDown.Clear();

    /// <summary>Returns true when WH_KEYBOARD_LL sent a repeat key-down for an already-held VK.</summary>
    public bool IsAutorepeatKeyDown(ushort virtualKey) => _keysDown.Contains(virtualKey);

    public void OnKeyDownStored(ushort virtualKey) => _keysDown.Add(virtualKey);

    public void OnKeyUpStored(ushort virtualKey) => _keysDown.Remove(virtualKey);
}
