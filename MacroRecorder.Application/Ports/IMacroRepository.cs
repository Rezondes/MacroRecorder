using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IMacroRepository
{
    Task<IReadOnlyList<MacroSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<Macro?> GetAsync(MacroId id, CancellationToken cancellationToken = default);

    Task SaveAsync(Macro macro, CancellationToken cancellationToken = default);

    Task DeleteAsync(MacroId id, CancellationToken cancellationToken = default);
}

public sealed record MacroSummary(MacroId Id, string Name, int EventCount, DateTimeOffset LastModifiedUtc);
