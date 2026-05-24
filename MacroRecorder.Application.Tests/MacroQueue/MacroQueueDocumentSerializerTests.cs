using MacroRecorder.Application.MacroQueue;
using MacroRecorder.Domain;
using MacroRecorder.Domain.MacroQueue;

namespace MacroRecorder.Application.Tests.MacroQueue;

public sealed class MacroQueueDocumentSerializerTests
{
    [Fact]
    public void Serialize_deserialize_round_trips_document()
    {
        var macroId = MacroId.New();
        var document = MacroQueueDocument.Create(
            "queue-a",
            [new QueueStep(macroId, RepeatCount: 2, InitialDelay: TimeSpan.FromSeconds(1))],
            loopWholeQueue: true);

        var json = MacroQueueDocumentSerializer.Serialize(document);
        var roundTrip = MacroQueueDocumentSerializer.Deserialize(json);

        Assert.Equal(document.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(document.Name, roundTrip.Name);
        Assert.Equal(document.LoopWholeQueue, roundTrip.LoopWholeQueue);
        Assert.Equal(macroId, roundTrip.Steps[0].MacroId);
        Assert.Equal(2, roundTrip.Steps[0].RepeatCount);
    }

    [Fact]
    public void Deserialize_skips_steps_with_empty_macro_id()
    {
        const string json =
            """
            {
              "schemaVersion": 1,
              "name": "q",
              "steps": [
                { "macroId": "", "repeatCount": 1 },
                { "macroId": "01ARZ3NDEKTSV4RRFFQ69G5FAV", "repeatCount": 1 }
              ]
            }
            """;

        var document = MacroQueueDocumentSerializer.Deserialize(json);

        Assert.Single(document.Steps);
    }

    [Fact]
    public void Deserialize_throws_for_unsupported_schema_version()
    {
        const string json =
            """
            {
              "schemaVersion": 99,
              "name": "q",
              "steps": []
            }
            """;

        Assert.Throws<InvalidOperationException>(() => MacroQueueDocumentSerializer.Deserialize(json));
    }
}
