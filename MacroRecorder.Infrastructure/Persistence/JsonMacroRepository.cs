using System.Text.Json;
using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;

namespace MacroRecorder.Infrastructure.Persistence;

public sealed class JsonMacroRepository : IMacroRepository
{
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

        var lastWriteUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        await using var stream = File.OpenRead(path);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return MacroJsonFileFormat.ParseMacro(document.RootElement, lastWriteUtc);
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
                    var lastWriteUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(macroFilePath), TimeSpan.Zero);
                    using var document = JsonDocument.Parse(fileJson);
                    var macro = MacroJsonFileFormat.ParseMacro(document.RootElement, lastWriteUtc);
                    if (macro is null)
                        continue;
                    var playbackDuration = PlaybackDurationEstimator.EstimateTotalPlaybackDuration(macro.Events);
                    summaries.Add(new MacroSummary(
                        macro.Id,
                        macro.Name,
                        TimelineActionRowCount.Count(macro.Events),
                        macro.LastModifiedAtUtc,
                        playbackDuration));
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
        var macroFilePath = PathFor(macro.Id);
        await using var stream = File.Create(macroFilePath);
        await JsonSerializer.SerializeAsync(
            stream,
            MacroJsonFileFormat.ToDto(macro),
            MacroJsonFileFormat.JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private string PathFor(MacroId id) => Path.Combine(_root, $"{id.ToFileStem()}.json");
}
