using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Persistence;

namespace MacroRecorder.Infrastructure.Tests.Persistence;

public sealed class MacroJsonFileFormatTests
{
    [Fact]
    public void Serialize_parse_round_trips_macro_with_events()
    {
        var macroId = MacroId.New();
        var macro = new Macro(
            macroId,
            "sample-macro",
            RecordingMetadata.ForNewSession(null),
            [
                new KeyDownRecordedEvent
                {
                    DelayBefore = TimeSpan.FromMilliseconds(10),
                    Sequence = 1,
                    Vk = 0x41,
                    ScanCode = 0,
                    IsExtendedKey = false,
                    IsAltDown = false,
                    IsInjected = false
                },
                new MouseMoveRecordedEvent
                {
                    DelayBefore = TimeSpan.FromMilliseconds(5),
                    Sequence = 2,
                    ScreenX = 100,
                    ScreenY = 200
                }
            ]);

        var json = MacroJsonFileFormat.Serialize(macro);
        var parsed = MacroJsonFileFormat.ParseMacro(System.Text.Json.JsonDocument.Parse(json).RootElement);

        Assert.NotNull(parsed);
        Assert.Equal(macro.Id, parsed!.Id);
        Assert.Equal(macro.Name, parsed.Name);
        Assert.Equal(2, parsed.Events.Count);
        Assert.Equal(1UL, parsed.Events[0].Sequence);
        Assert.Equal(2UL, parsed.Events[1].Sequence);
    }

    [Fact]
    public void ParseMacro_returns_null_for_non_object_root()
    {
        using var document = System.Text.Json.JsonDocument.Parse("[]");

        var parsed = MacroJsonFileFormat.ParseMacro(document.RootElement);

        Assert.Null(parsed);
    }

    [Fact]
    public void ParseMacro_throws_for_corrupt_id_shape()
    {
        const string json =
            """
            {
              "id": 12345,
              "name": "x",
              "metadata": { "schemaVersion": 2, "recordedAtUtc": "2020-01-01T00:00:00Z", "stopwatchFrequency": 10000000 },
              "events": []
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);

        Assert.ThrowsAny<Exception>(() =>
            MacroJsonFileFormat.ParseMacro(document.RootElement));
    }
}
