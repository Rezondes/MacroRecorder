namespace MacroRecorder.Application.Ports;

/// <summary>
/// Host for the macro JSON import modal (UI thread). <paramref name="importJsonAsync"/> parses and saves; returns true to close the modal.
/// </summary>
public interface IImportMacroJsonModalHost
{
    void ShowImportMacroModal(Func<string, Task<bool>> importJsonAsync);
}
