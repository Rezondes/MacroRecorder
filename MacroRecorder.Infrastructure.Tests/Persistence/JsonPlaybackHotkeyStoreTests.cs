using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroRecorder.Infrastructure.Tests.Persistence;

public sealed class JsonPlaybackHotkeyStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hotkey-store-{Guid.NewGuid():N}");
    private readonly JsonPlaybackHotkeyStore _store;

    public JsonPlaybackHotkeyStoreTests()
    {
        _store = new JsonPlaybackHotkeyStore(NullLogger<JsonPlaybackHotkeyStore>.Instance, _root);
    }

    [Fact]
    public async Task Set_load_round_trips_assignment()
    {
        var macroId = MacroId.New();
        var chord = new PlaybackKeyChord(true, false, false, false, 0x50);

        await _store.SetAsync(macroId, chord);
        var loaded = await _store.LoadAsync();

        Assert.True(loaded.TryGetValue(macroId, out var loadedChord));
        Assert.Equal(chord, loadedChord);
    }

    [Fact]
    public async Task RemoveAsync_clears_assignment()
    {
        var macroId = MacroId.New();
        var chord = new PlaybackKeyChord(false, false, false, false, 0x51);
        await _store.SetAsync(macroId, chord);

        await _store.RemoveAsync(macroId);
        var loaded = await _store.LoadAsync();

        Assert.DoesNotContain(macroId, loaded.Keys);
    }

    [Fact]
    public async Task SetAsync_throws_when_chord_has_no_non_modifier_key()
    {
        var macroId = MacroId.New();
        var invalidChord = new PlaybackKeyChord(true, false, false, false, 0);

        await Assert.ThrowsAsync<ArgumentException>(() => _store.SetAsync(macroId, invalidChord));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
