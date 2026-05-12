using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

public interface INavigationService
{
    void OpenEditor(MacroId id);

    void OpenRecording(Action? onClosed = null);
}
