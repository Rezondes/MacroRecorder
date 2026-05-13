using MacroRecorder.Domain;

namespace MacroRecorder.Application.MacroQueue;

/// <summary>Thrown when a queue step references a macro that is not on disk.</summary>
public sealed class MacroQueueMissingMacroException : InvalidOperationException
{
    public MacroQueueMissingMacroException(MacroId macroId, string? message = null)
        : base(message ?? $"Macro not found: {macroId}")
    {
        MacroId = macroId;
    }

    public MacroId MacroId { get; }
}
