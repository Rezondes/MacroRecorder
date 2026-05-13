using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacroRecorder.Application.Timeline;
using MacroRecorder.Domain;

namespace MacroRecorder.Infrastructure.Persistence;

internal sealed class MacroFileDto
{
    public string Id { get; set; } = "";
    public string DocumentVersion { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastModifiedAtUtc { get; set; }
    public RecordingMetadata Metadata { get; set; } = null!;
    public List<RecordedInputEvent> Events { get; set; } = new();
    public bool WasModifiedAfterRecording { get; set; }
}

/// <summary>Same JSON shape as on-disk macro files (<c>*.json</c> under LocalAppData).</summary>
public static class MacroJsonFileFormat
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(Macro macro) =>
        JsonSerializer.Serialize(ToDto(macro), JsonOptions);

    internal static MacroFileDto ToDto(Macro macro) =>
        new()
        {
            Id = macro.Id.ToString(),
            DocumentVersion = macro.DocumentVersion.ToString(),
            Name = macro.Name,
            CreatedAtUtc = macro.CreatedAtUtc,
            LastModifiedAtUtc = macro.LastModifiedAtUtc,
            Metadata = macro.Metadata with { SchemaVersion = RecordingMetadata.CurrentSchemaVersion },
            Events = macro.Events.ToList(),
            WasModifiedAfterRecording = macro.WasModifiedAfterRecording
        };

    public static async Task<Macro?> DeserializeMacroAsync(Stream stream,
        CancellationToken cancellationToken = default)
    {
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ParseMacro(document.RootElement);
    }

    public static Macro? ParseMacro(JsonElement root, DateTimeOffset? fileLastWriteUtc = null)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var idString = ReadIdString(root.GetProperty("id"));
        var name = root.GetProperty("name").GetString() ?? "";
        var metadata = root.TryGetProperty("metadata", out var metaElement)
            ? JsonSerializer.Deserialize<RecordingMetadata>(metaElement.GetRawText(), JsonOptions)
              ?? RecordingMetadata.Empty()
            : RecordingMetadata.Empty();
        var wasModified = root.TryGetProperty("wasModifiedAfterRecording", out var w) &&
                          w.ValueKind == JsonValueKind.True;

        Ulid documentVersion = Ulid.NewUlid();
        if (root.TryGetProperty("documentVersion", out var docVerEl) &&
            docVerEl.ValueKind == JsonValueKind.String)
        {
            var dv = docVerEl.GetString();
            if (!string.IsNullOrWhiteSpace(dv) && Ulid.TryParse(dv, null, out var parsed))
                documentVersion = parsed;
        }

        var createdAtUtc = ReadDateTimeOffsetOrDefault(
            root,
            "createdAtUtc",
            defaultWhenMissing: metadata.RecordedAtUtc != DateTimeOffset.MinValue
                ? metadata.RecordedAtUtc
                : fileLastWriteUtc ?? DateTimeOffset.UtcNow);

        var lastModifiedAtUtc = ReadDateTimeOffsetOrDefault(
            root,
            "lastModifiedAtUtc",
            defaultWhenMissing: fileLastWriteUtc ?? DateTimeOffset.UtcNow);

        if (!root.TryGetProperty("events", out var eventsElement))
            return new Macro(
                MacroId.Parse(idString),
                name,
                metadata,
                Array.Empty<RecordedInputEvent>(),
                wasModified,
                documentVersion,
                createdAtUtc,
                lastModifiedAtUtc);

        var events = MacroJsonEventsArrayDeserializer.Deserialize(eventsElement, JsonOptions);
        TimelineNormalizer.NormalizeInPlace(events);
        return new Macro(
            MacroId.Parse(idString),
            name,
            metadata,
            events,
            wasModified,
            documentVersion,
            createdAtUtc,
            lastModifiedAtUtc);
    }

    private static string ReadIdString(JsonElement idElement)
    {
        if (idElement.ValueKind == JsonValueKind.String)
            return idElement.GetString() ?? "";
        if (idElement.TryGetGuid(out var guid))
            return guid.ToString("D");
        throw new JsonException("Macro id must be a JSON string (ULID or GUID) or a GUID value.");
    }

    private static DateTimeOffset ReadDateTimeOffsetOrDefault(
        JsonElement root,
        string propertyName,
        DateTimeOffset defaultWhenMissing)
    {
        if (!root.TryGetProperty(propertyName, out var p))
            return defaultWhenMissing;
        if (p.ValueKind == JsonValueKind.String)
            return DateTimeOffset.Parse(p.GetString()!, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        return p.GetDateTimeOffset();
    }
}
