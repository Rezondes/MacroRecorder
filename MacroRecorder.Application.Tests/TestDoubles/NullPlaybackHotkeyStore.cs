using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.TestDoubles;

internal sealed class NullPlaybackHotkeyStore : IPlaybackHotkeyStore
{
    public Task<IReadOnlyDictionary<MacroId, PlaybackKeyChord>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<MacroId, PlaybackKeyChord>>(new Dictionary<MacroId, PlaybackKeyChord>());

    public Task RemoveAsync(MacroId macroId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetAsync(MacroId macroId, PlaybackKeyChord? chord, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
