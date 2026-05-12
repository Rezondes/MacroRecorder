using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

public interface INavigationService
{
    void OpenEditor(MacroId id, Action? onMacroSaved = null);

    /// <summary>Opens the editor for a macro that is not yet persisted to disk.</summary>
    void OpenNewMacroEditor(Macro macro, Action? onMacroSaved = null);

    void OpenRecording(Action? onClosed = null, Action? onMacroSavedFromEditor = null);
}
