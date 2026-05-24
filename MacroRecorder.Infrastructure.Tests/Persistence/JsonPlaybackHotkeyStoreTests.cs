using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroRecorder.Infrastructure.Tests.Persistence;

public sealed class JsonPlaybackHotkeyStoreTests : IDisposable
{
    private readonly string _appRoot = Path.Combine(Path.GetTempPath(), $"hotkey-store-{Guid.NewGuid():N}");
    private readonly JsonPlaybackHotkeyStore _store;

    public JsonPlaybackHotkeyStoreTests()
    {
        _store = new JsonPlaybackHotkeyStore(NullLogger<JsonPlaybackHotkeyStore>.Instance, _appRoot);
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
        Assert.True(File.Exists(Path.Combine(_appRoot, "playback-hotkeys.json")));
        Assert.False(File.Exists(Path.Combine(_appRoot, "macros", "playback-hotkeys.json")));
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

    [Fact]
    public async Task LoadAsync_migrates_legacy_macros_folder_file_to_app_root()
    {
        var legacyDir = Path.Combine(_appRoot, "macros");
        Directory.CreateDirectory(legacyDir);
        var macroId = MacroId.New();
        var legacyPath = Path.Combine(legacyDir, "playback-hotkeys.json");
        var legacyJson =
            $$"""{"assignments":[{"macroId":"{{macroId.Value}}","ctrl":true,"alt":false,"shift":false,"win":false,"vk":80}]}""";
        await File.WriteAllTextAsync(legacyPath, legacyJson);

        var migratedStore = new JsonPlaybackHotkeyStore(NullLogger<JsonPlaybackHotkeyStore>.Instance, _appRoot);
        var loaded = await migratedStore.LoadAsync();

        Assert.True(loaded.TryGetValue(macroId, out var loadedChord));
        Assert.Equal(new PlaybackKeyChord(true, false, false, false, 0x50), loadedChord);
        Assert.True(File.Exists(Path.Combine(_appRoot, "playback-hotkeys.json")));
        Assert.False(File.Exists(legacyPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_appRoot))
            Directory.Delete(_appRoot, recursive: true);
    }
}
