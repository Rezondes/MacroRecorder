using MacroRecorder.Domain;

namespace MacroRecorder.Application.MacroQueue;

/// <summary>Thrown when a queue step references a macro with no playable events.</summary>
public sealed class MacroQueueEmptyMacroException : InvalidOperationException
{
    public MacroQueueEmptyMacroException(MacroId macroId)
        : base($"Macro has no events: {macroId}")
    {
        MacroId = macroId;
    }

    public MacroId MacroId { get; }
}
