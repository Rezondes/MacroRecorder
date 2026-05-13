using System.Text.Json;
using System.Text.Json.Serialization;
using MacroRecorder.Domain;
using MacroRecorder.Domain.MacroQueue;

namespace MacroRecorder.Application.MacroQueue;

public static class MacroQueueDocumentSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(MacroQueueDocument document)
    {
        var dto = ToDto(document);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static MacroQueueDocument Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<MacroQueueFileDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Queue file is empty or invalid JSON.");
        return FromDto(dto);
    }

    private static MacroQueueFileDto ToDto(MacroQueueDocument document)
    {
        var steps = document.Steps.Select(static step => new QueueStepDto
        {
            MacroId = step.MacroId.Value,
            RepeatCount = step.RepeatCount,
            InitialDelay = step.InitialDelay,
            DelayBetweenRuns = step.DelayBetweenRuns,
            PostStepDelay = step.PostStepDelay
        }).ToList();

        return new MacroQueueFileDto
        {
            SchemaVersion = document.SchemaVersion,
            Name = document.Name,
            Steps = steps,
            LoopWholeQueue = document.LoopWholeQueue
        };
    }

    private static MacroQueueDocument FromDto(MacroQueueFileDto dto)
    {
        var version = dto.SchemaVersion <= 0 ? MacroQueueDocument.CurrentSchemaVersion : dto.SchemaVersion;
        if (version > MacroQueueDocument.CurrentSchemaVersion)
            throw new InvalidOperationException($"Unsupported queue schema version: {version}.");

        var name = dto.Name ?? "";
        var steps = new List<QueueStep>();
        foreach (var stepDto in dto.Steps ?? [])
        {
            if (string.IsNullOrWhiteSpace(stepDto.MacroId))
                continue;
            var macroId = MacroId.Parse(stepDto.MacroId);
            var repeat = stepDto.RepeatCount < 1 ? 1 : stepDto.RepeatCount;
            steps.Add(new QueueStep(
                macroId,
                repeat,
                stepDto.InitialDelay < TimeSpan.Zero ? TimeSpan.Zero : stepDto.InitialDelay,
                stepDto.DelayBetweenRuns < TimeSpan.Zero ? TimeSpan.Zero : stepDto.DelayBetweenRuns,
                stepDto.PostStepDelay < TimeSpan.Zero ? TimeSpan.Zero : stepDto.PostStepDelay));
        }

        return new MacroQueueDocument(version, name, steps, dto.LoopWholeQueue).WithValidatedSteps();
    }

    private sealed class MacroQueueFileDto
    {
        public int SchemaVersion { get; set; } = 1;
        public string? Name { get; set; }
        public List<QueueStepDto>? Steps { get; set; }
        public bool LoopWholeQueue { get; set; }
    }

    private sealed class QueueStepDto
    {
        public string? MacroId { get; set; }
        public int RepeatCount { get; set; } = 1;
        public TimeSpan InitialDelay { get; set; }
        public TimeSpan DelayBetweenRuns { get; set; }
        public TimeSpan PostStepDelay { get; set; }
    }
}
