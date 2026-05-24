using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroRecorder.Infrastructure.Tests.Persistence;

public sealed class JsonMacroRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"macro-repo-{Guid.NewGuid():N}");
    private readonly JsonMacroRepository _repository;

    public JsonMacroRepositoryTests()
    {
        _repository = new JsonMacroRepository(NullLogger<JsonMacroRepository>.Instance, _root);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_macro_file_missing()
    {
        var loaded = await _repository.GetAsync(MacroId.New());

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Save_get_round_trips_macro()
    {
        var macro = Macro.CreateEmpty("stored");

        await _repository.SaveAsync(macro);
        var loaded = await _repository.GetAsync(macro.Id);

        Assert.NotNull(loaded);
        Assert.Equal(macro.Id, loaded!.Id);
        Assert.Equal("stored", loaded.Name);
    }

    [Fact]
    public async Task ListAsync_returns_saved_macro_summary()
    {
        var macro = Macro.CreateEmpty("listed");
        macro.AppendEvent(new KeyDownRecordedEvent
        {
            DelayBefore = TimeSpan.FromMilliseconds(10),
            Sequence = 1,
            Vk = 0x41,
            ScanCode = 0,
            IsExtendedKey = false,
            IsAltDown = false,
            IsInjected = false
        });
        await _repository.SaveAsync(macro);

        var summaries = await _repository.ListAsync();

        Assert.Contains(summaries, summary => summary.Id == macro.Id && summary.Name == "listed");
    }

    [Fact]
    public async Task DeleteAsync_removes_macro_file()
    {
        var macro = Macro.CreateEmpty("deleted");
        await _repository.SaveAsync(macro);

        await _repository.DeleteAsync(macro.Id);

        Assert.Null(await _repository.GetAsync(macro.Id));
    }

    [Fact]
    public async Task SaveDisplayOrderAsync_orders_list_results()
    {
        var first = Macro.CreateEmpty("first");
        var second = Macro.CreateEmpty("second");
        await _repository.SaveAsync(first);
        await _repository.SaveAsync(second);
        await _repository.SaveDisplayOrderAsync([second.Id, first.Id]);

        var summaries = await _repository.ListAsync();

        Assert.Equal(second.Id, summaries[0].Id);
        Assert.Equal(first.Id, summaries[1].Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
