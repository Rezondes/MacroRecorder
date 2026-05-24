using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.TestDoubles;

internal sealed class InMemoryMacroRepository : IMacroRepository
{
    private readonly Dictionary<string, Macro> _macros = new(StringComparer.Ordinal);

    public Task DeleteAsync(MacroId id, CancellationToken cancellationToken = default)
    {
        _macros.Remove(id.Value);
        return Task.CompletedTask;
    }

    public Task<Macro?> GetAsync(MacroId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_macros.TryGetValue(id.Value, out var macro) ? macro : null);

    public Task<IReadOnlyList<MacroSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MacroSummary> summaries = _macros.Values
            .Select(macro => new MacroSummary(
                macro.Id,
                macro.Name,
                macro.Events.Count,
                macro.LastModifiedAtUtc,
                TimeSpan.Zero))
            .ToList();
        return Task.FromResult(summaries);
    }

    public Task SaveAsync(Macro macro, CancellationToken cancellationToken = default)
    {
        _macros[macro.Id.Value] = macro;
        return Task.CompletedTask;
    }

    public Task SaveDisplayOrderAsync(IReadOnlyList<MacroId> orderedIds, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
