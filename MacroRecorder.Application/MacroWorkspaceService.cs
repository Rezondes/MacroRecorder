using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.Application;

public sealed class MacroWorkspaceService(IMacroRepository repository)
{
    public Task<IReadOnlyList<MacroSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(cancellationToken);

    public Task<Macro?> GetAsync(MacroId id, CancellationToken cancellationToken = default) =>
        repository.GetAsync(id, cancellationToken);

    public Task SaveAsync(Macro macro, CancellationToken cancellationToken = default) =>
        repository.SaveAsync(macro, cancellationToken);

    public Task DeleteAsync(MacroId id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);
}
