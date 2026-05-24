using MacroRecorder.Domain.MacroQueue;

namespace MacroRecorder.Domain.Tests.MacroQueue;

public sealed class MacroQueueDocumentTests
{
    [Fact]
    public void Create_sets_current_schema_version()
    {
        var macroId = MacroId.New();
        var document = MacroQueueDocument.Create("queue-a", [new QueueStep(macroId)]);

        Assert.Equal(MacroQueueDocument.CurrentSchemaVersion, document.SchemaVersion);
        Assert.Equal("queue-a", document.Name);
        Assert.Single(document.Steps);
    }

    [Fact]
    public void WithValidatedSteps_clamps_repeat_count_below_one()
    {
        var macroId = MacroId.New();
        var document = new MacroQueueDocument(1, "q", [new QueueStep(macroId, RepeatCount: 0)]);

        var validated = document.WithValidatedSteps();

        Assert.Equal(1, validated.Steps[0].RepeatCount);
    }

    [Fact]
    public void WithValidatedSteps_preserves_valid_repeat_count()
    {
        var macroId = MacroId.New();
        var document = new MacroQueueDocument(1, "q", [new QueueStep(macroId, RepeatCount: 3)]);

        var validated = document.WithValidatedSteps();

        Assert.Equal(3, validated.Steps[0].RepeatCount);
    }
}
