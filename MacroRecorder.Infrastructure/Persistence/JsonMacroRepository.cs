using System.Text.Json;
using MacroRecorder.Application.Ports;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;

namespace MacroRecorder.Infrastructure.Persistence;

public sealed class JsonMacroRepository : IMacroRepository
{
    private const string OverviewOrderFileName = "overview-order.json";

    private static readonly JsonSerializerOptions OrderJsonOptions = new()
    {
        WriteIndented = true
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
            RemoveIdFromOrder(id);
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
                if (string.Equals(Path.GetFileName(macroFilePath), OverviewOrderFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

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

            var savedOrder = LoadOrderIds();
            return MergeDisplayOrder(summaries, savedOrder);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(Macro macro, CancellationToken cancellationToken = default)
    {
        var macroFilePath = PathFor(macro.Id);
        var wasNew = !File.Exists(macroFilePath);
        await using (var stream = File.Create(macroFilePath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                MacroJsonFileFormat.ToDto(macro),
                MacroJsonFileFormat.JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        if (wasNew)
            await Task.Run(() => AppendIdToOrderIfMissing(macro.Id), cancellationToken).ConfigureAwait(false);
    }

    public Task SaveDisplayOrderAsync(IReadOnlyList<MacroId> orderedIds, CancellationToken cancellationToken = default) =>
        Task.Run(() => WriteOrderFromIds(orderedIds), cancellationToken);

    private string OrderFilePath => Path.Combine(_root, OverviewOrderFileName);

    private List<MacroId> LoadOrderIds()
    {
        try
        {
            if (!File.Exists(OrderFilePath))
                return new List<MacroId>();

            var json = File.ReadAllText(OrderFilePath);
            var strings = JsonSerializer.Deserialize<List<string>>(json, OrderJsonOptions);
            if (strings is null)
                return new List<MacroId>();

            var list = new List<MacroId>();
            foreach (var s in strings)
            {
                if (string.IsNullOrWhiteSpace(s))
                    continue;
                try
                {
                    list.Add(MacroId.Parse(s));
                }
                catch (FormatException)
                {
                    // skip invalid ids
                }
            }

            return list;
        }
        catch
        {
            return new List<MacroId>();
        }
    }

    private void WriteOrderFromIds(IReadOnlyList<MacroId> orderedIds)
    {
        var distinctValues = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in orderedIds)
        {
            if (seen.Add(id.Value))
                distinctValues.Add(id.Value);
        }

        WriteOrderValues(distinctValues);
    }

    private void WriteOrderValues(IReadOnlyList<string> orderedIdValues)
    {
        var path = OrderFilePath;
        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(orderedIdValues.ToArray(), OrderJsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    private void AppendIdToOrderIfMissing(MacroId id)
    {
        var order = LoadOrderIds();
        if (order.Any(existing => existing.Value == id.Value))
            return;
        order.Add(id);
        WriteOrderFromIds(order);
    }

    private void RemoveIdFromOrder(MacroId id)
    {
        var order = LoadOrderIds();
        var filtered = order.Where(x => x.Value != id.Value).ToList();
        if (filtered.Count == order.Count)
            return;
        WriteOrderFromIds(filtered);
    }

    private static List<MacroSummary> MergeDisplayOrder(List<MacroSummary> summaries, IReadOnlyList<MacroId> savedOrder)
    {
        if (summaries.Count == 0)
            return summaries;

        var byId = summaries.ToDictionary(s => s.Id, s => s);
        var result = new List<MacroSummary>(summaries.Count);
        var placed = new HashSet<MacroId>();

        foreach (var id in savedOrder)
        {
            if (!byId.TryGetValue(id, out var summary) || !placed.Add(id))
                continue;
            result.Add(summary);
        }

        foreach (var summary in byId.Values
                     .Where(s => !placed.Contains(s.Id))
                     .OrderBy(s => s.LastModifiedUtc)
                     .ThenBy(s => s.Id.Value, StringComparer.Ordinal))
        {
            result.Add(summary);
        }

        return result;
    }

    private string PathFor(MacroId id) => Path.Combine(_root, $"{id.ToFileStem()}.json");
}
