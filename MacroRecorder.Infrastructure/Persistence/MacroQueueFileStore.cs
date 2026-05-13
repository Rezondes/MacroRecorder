using MacroRecorder.Application.MacroQueue;
using MacroRecorder.Domain.MacroQueue;

namespace MacroRecorder.Infrastructure.Persistence;

/// <summary>Reads/writes queue JSON files under LocalAppData (sibling folder to macros).</summary>
public sealed class MacroQueueFileStore
{
    private readonly string _root;

    public MacroQueueFileStore()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroRecorderByRezondes",
            "queues");
        Directory.CreateDirectory(_root);
    }

    public string RootDirectory => _root;

    public async Task SaveAsync(string fullPath, MacroQueueDocument document, CancellationToken cancellationToken = default)
    {
        var json = MacroQueueDocumentSerializer.Serialize(document);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(fullPath, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MacroQueueDocument> LoadAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(fullPath);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return MacroQueueDocumentSerializer.Deserialize(json);
    }
}
