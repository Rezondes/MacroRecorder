using MacroRecorder.App.ViewModels;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.App.Services;

public sealed class ShellNavigationService(ShellViewModel shell, IServiceProvider serviceProvider) : INavigationService
{
    public void OpenEditor(MacroId id, Action? onMacroSaved = null)
    {
        var editor = shell.CreateEditorViewModel(id, loadFromDisk: true, inMemoryMacro: null, onMacroSaved);
        if (shell.CurrentPage is RecordViewModel)
            shell.ReplaceTop(editor);
        else
            shell.PushPage(editor);
    }

    public void OpenNewMacroEditor(Macro macro, Action? onMacroSaved = null)
    {
        var editor = shell.CreateEditorViewModel(macro.Id, loadFromDisk: false, macro, onMacroSaved);
        shell.PushPage(editor);
    }

    public void OpenRecording(Action? onClosed = null, Action? onMacroSavedFromEditor = null)
    {
        var recordViewModel = new RecordViewModel(
            serviceProvider.GetRequiredService<RecordingCoordinator>(),
            serviceProvider.GetRequiredService<MacroWorkspaceService>(),
            serviceProvider.GetRequiredService<IUserDialogService>(),
            serviceProvider.GetRequiredService<INavigationService>(),
            serviceProvider.GetRequiredService<IUiLocalizer>(),
            onMacroSavedFromEditor);
        shell.PushPage(recordViewModel);
        // onClosed: previously RecordWindow.Closed; not wired for shell-hosted record page (unused entry point today).
    }
}
