using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

/// <summary>Another macro already owns this key combination.</summary>
public sealed class PlaybackHotkeyConflictException : Exception
{
    public PlaybackHotkeyConflictException(MacroId conflictingMacroId)
        : base(conflictingMacroId.ToString()) =>
        ConflictingMacroId = conflictingMacroId;

    public MacroId ConflictingMacroId { get; }
}
