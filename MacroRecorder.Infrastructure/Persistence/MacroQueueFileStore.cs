using MacroRecorder.Application.MacroQueue;
using MacroRecorder.Domain.MacroQueue;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.Infrastructure.Persistence;

/// <summary>Reads/writes queue JSON files under LocalAppData (sibling folder to macros).</summary>
public sealed class MacroQueueFileStore
{
    private readonly ILogger<MacroQueueFileStore> _logger;
    private readonly string _root;

    public MacroQueueFileStore(ILogger<MacroQueueFileStore> logger, string? rootOverride = null)
    {
        _logger = logger;
        _root = rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroRecorderByRezondes",
            "queues");
        Directory.CreateDirectory(_root);
    }

    public string RootDirectory => _root;

    public async Task SaveAsync(string fullPath, MacroQueueDocument document, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = MacroQueueDocumentSerializer.Serialize(document);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(fullPath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save macro queue file at {FilePath}", fullPath);
            throw;
        }
    }

    public async Task<MacroQueueDocument> LoadAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(fullPath);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return MacroQueueDocumentSerializer.Deserialize(json);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load macro queue file at {FilePath}", fullPath);
            throw;
        }
    }
}
