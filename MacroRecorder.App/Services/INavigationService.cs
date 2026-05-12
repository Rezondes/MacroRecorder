using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

public interface INavigationService
{
    void OpenEditor(MacroId id);

    /// <summary>Opens the editor for a macro that is not yet persisted to disk.</summary>
    void OpenNewMacroEditor(Macro macro);

    void OpenRecording(Action? onClosed = null);
}
