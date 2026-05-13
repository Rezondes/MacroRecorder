using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

/// <summary>Persists global playback shortcuts per macro (separate from macro JSON timeline).</summary>
public interface IPlaybackHotkeyStore
{
    Task<IReadOnlyDictionary<MacroId, PlaybackKeyChord>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets or clears a shortcut. Throws <see cref="PlaybackHotkeyConflictException"/> if <paramref name="chord"/> is already used by another macro.</summary>
    Task SetAsync(MacroId macroId, PlaybackKeyChord? chord, CancellationToken cancellationToken = default);

    Task RemoveAsync(MacroId macroId, CancellationToken cancellationToken = default);
}
