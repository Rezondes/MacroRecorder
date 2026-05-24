using System.Text.Json;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.Infrastructure.Persistence;

public sealed class JsonPlaybackHotkeyStore : IPlaybackHotkeyStore
{
    private const string FileName = "playback-hotkeys.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<JsonPlaybackHotkeyStore> _logger;
    private readonly string _filePath;

    public JsonPlaybackHotkeyStore(ILogger<JsonPlaybackHotkeyStore> logger, string? rootOverride = null)
    {
        _logger = logger;
        var root = rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroRecorderByRezondes",
            "macros");
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, FileName);
    }

    public Task<IReadOnlyDictionary<MacroId, PlaybackKeyChord>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(ReadAll, cancellationToken);

    public Task SetAsync(MacroId macroId, PlaybackKeyChord? chord, CancellationToken cancellationToken = default) =>
        Task.Run(() => SetCore(macroId, chord), cancellationToken);

    public Task RemoveAsync(MacroId macroId, CancellationToken cancellationToken = default) =>
        SetAsync(macroId, null, cancellationToken);

    private IReadOnlyDictionary<MacroId, PlaybackKeyChord> ReadAll()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<MacroId, PlaybackKeyChord>();

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<HotkeyFileDto>(json, JsonOptions);
            if (dto?.Assignments is null || dto.Assignments.Count == 0)
                return new Dictionary<MacroId, PlaybackKeyChord>();

            var map = new Dictionary<MacroId, PlaybackKeyChord>();
            foreach (var entry in dto.Assignments)
            {
                if (string.IsNullOrWhiteSpace(entry.MacroId))
                    continue;
                try
                {
                    var id = MacroId.Parse(entry.MacroId);
                    var chord = new PlaybackKeyChord(entry.Ctrl, entry.Alt, entry.Shift, entry.Win, entry.Vk);
                    if (!chord.HasNonModifierKey)
                        continue;
                    map[id] = chord;
                }
                catch (FormatException)
                {
                    // skip bad id
                }
            }

            return map;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load playback hotkeys from {FilePath}", _filePath);
            return new Dictionary<MacroId, PlaybackKeyChord>();
        }
    }

    private void SetCore(MacroId macroId, PlaybackKeyChord? chord)
    {
        var map = new Dictionary<MacroId, PlaybackKeyChord>(ReadAll());

        map.Remove(macroId);

        if (chord is { } c)
        {
            if (!c.HasNonModifierKey)
                throw new ArgumentException("Chord must include a non-zero virtual key.", nameof(chord));

            foreach (var (existingId, existingChord) in map)
            {
                if (existingId.Value == macroId.Value)
                    continue;
                if (ChordsEqual(existingChord, c))
                    throw new PlaybackHotkeyConflictException(existingId);
            }

            map[macroId] = c;
        }

        WriteAll(map);
    }

    private void WriteAll(IReadOnlyDictionary<MacroId, PlaybackKeyChord> map)
    {
        try
        {
            var dto = new HotkeyFileDto
            {
                Assignments = map
                    .Select(static pair => new HotkeyEntryDto
                    {
                        MacroId = pair.Key.Value,
                        Ctrl = pair.Value.Ctrl,
                        Alt = pair.Value.Alt,
                        Shift = pair.Value.Shift,
                        Win = pair.Value.Win,
                        Vk = pair.Value.VirtualKey
                    })
                    .ToList()
            };

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(dto, JsonOptions));
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save playback hotkeys to {FilePath}", _filePath);
            throw;
        }
    }

    private static bool ChordsEqual(PlaybackKeyChord a, PlaybackKeyChord b) =>
        a.Ctrl == b.Ctrl && a.Alt == b.Alt && a.Shift == b.Shift && a.Win == b.Win && a.VirtualKey == b.VirtualKey;

    private sealed class HotkeyFileDto
    {
        public List<HotkeyEntryDto> Assignments { get; set; } = new();
    }

    private sealed class HotkeyEntryDto
    {
        public string MacroId { get; set; } = "";
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public uint Vk { get; set; }
    }
}
