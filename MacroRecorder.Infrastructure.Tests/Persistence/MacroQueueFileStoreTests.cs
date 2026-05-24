using MacroRecorder.Domain;
using MacroRecorder.Domain.MacroQueue;
using MacroRecorder.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroRecorder.Infrastructure.Tests.Persistence;

public sealed class MacroQueueFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"queue-store-{Guid.NewGuid():N}");
    private readonly MacroQueueFileStore _store;

    public MacroQueueFileStoreTests()
    {
        Directory.CreateDirectory(_root);
        _store = new MacroQueueFileStore(NullLogger<MacroQueueFileStore>.Instance, _root);
    }

    [Fact]
    public async Task Save_load_round_trips_queue_document()
    {
        var macroId = MacroId.New();
        var document = MacroQueueDocument.Create("queue-b", [new QueueStep(macroId, RepeatCount: 2)]);
        var filePath = Path.Combine(_root, "queue-b.json");

        await _store.SaveAsync(filePath, document);
        var loaded = await _store.LoadAsync(filePath);

        Assert.Equal(document.Name, loaded.Name);
        Assert.Equal(macroId, loaded.Steps[0].MacroId);
        Assert.Equal(2, loaded.Steps[0].RepeatCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
