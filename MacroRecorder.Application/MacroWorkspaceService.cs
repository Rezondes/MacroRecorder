using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.Application;

public sealed class MacroWorkspaceService(IMacroRepository repository, IPlaybackHotkeyStore playbackHotkeys)
{
    public async Task<IReadOnlyList<MacroSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        var hotkeys = await playbackHotkeys.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (hotkeys.Count == 0)
            return list;

        return list
            .Select(s => hotkeys.TryGetValue(s.Id, out var chord)
                ? s with { PlaybackHotkey = chord }
                : s)
            .ToList();
    }

    public Task<Macro?> GetAsync(MacroId id, CancellationToken cancellationToken = default) =>
        repository.GetAsync(id, cancellationToken);

    public Task SaveAsync(Macro macro, CancellationToken cancellationToken = default) =>
        repository.SaveAsync(macro, cancellationToken);

    public async Task DeleteAsync(MacroId id, CancellationToken cancellationToken = default)
    {
        await playbackHotkeys.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveMacroDisplayOrderAsync(IReadOnlyList<MacroId> orderedIds, CancellationToken cancellationToken = default) =>
        repository.SaveDisplayOrderAsync(orderedIds, cancellationToken);

    public Task SetPlaybackHotkeyAsync(MacroId macroId, PlaybackKeyChord? chord, CancellationToken cancellationToken = default) =>
        playbackHotkeys.SetAsync(macroId, chord, cancellationToken);
}
