using System.Text.Json;
using System.Text.Json.Serialization;
using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;

namespace MacroRecorder.Infrastructure.Persistence;

internal sealed class MacroFileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public RecordingMetadata Metadata { get; set; } = null!;
    public List<RecordedInputEvent> Events { get; set; } = new();
    public bool WasModifiedAfterRecording { get; set; }
}

public sealed class JsonMacroRepository : IMacroRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _root;

    public JsonMacroRepository()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroRecorderByRezondes",
            "macros");
        Directory.CreateDirectory(_root);
    }

    public async Task DeleteAsync(MacroId id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        await Task.Run(() =>
        {
            if (File.Exists(path))
                File.Delete(path);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Macro?> GetAsync(MacroId id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        var macroFileDto = await JsonSerializer.DeserializeAsync<MacroFileDto>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (macroFileDto is null)
            return null;

        return new Macro(
            new MacroId(macroFileDto.Id),
            macroFileDto.Name,
            macroFileDto.Metadata ?? RecordingMetadata.Empty(),
            macroFileDto.Events,
            macroFileDto.WasModifiedAfterRecording);
    }

    public async Task<IReadOnlyList<MacroSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var summaries = new List<MacroSummary>();
            foreach (var macroFilePath in Directory.EnumerateFiles(_root, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fileJson = File.ReadAllText(macroFilePath);
                    var macroFileDto = JsonSerializer.Deserialize<MacroFileDto>(fileJson, JsonOptions);
                    if (macroFileDto is null)
                        continue;
                    var lastWriteTimeUtc = File.GetLastWriteTimeUtc(macroFilePath);
                    summaries.Add(new MacroSummary(
                        new MacroId(macroFileDto.Id),
                        macroFileDto.Name,
                        TimelineActionRowCount.Count(macroFileDto.Events),
                        new DateTimeOffset(lastWriteTimeUtc, TimeSpan.Zero)));
                }
                catch
                {
                    // skip corrupt files
                }
            }

            return summaries.OrderByDescending(macroSummary => macroSummary.LastModifiedUtc).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(Macro macro, CancellationToken cancellationToken = default)
    {
        var macroFileDto = new MacroFileDto
        {
            Id = macro.Id.Value,
            Name = macro.Name,
            Metadata = macro.Metadata,
            Events = macro.Events.ToList(),
            WasModifiedAfterRecording = macro.WasModifiedAfterRecording
        };

        var macroFilePath = PathFor(macro.Id);
        await using var stream = File.Create(macroFilePath);
        await JsonSerializer.SerializeAsync(stream, macroFileDto, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private string PathFor(MacroId id) => Path.Combine(_root, $"{id.Value:N}.json");
}
