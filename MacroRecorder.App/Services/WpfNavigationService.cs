using System.Windows;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.App.Services;

public sealed class WpfNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public void OpenEditor(MacroId id, Action? onMacroSaved = null)
    {
        var editorViewModel = CreateEditorViewModel(id, loadFromDisk: true, inMemoryMacro: null, onMacroSaved);
        var editorWindow = new MacroEditorWindow(editorViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        editorWindow.Show();
    }

    public void OpenNewMacroEditor(Macro macro, Action? onMacroSaved = null)
    {
        var editorViewModel = CreateEditorViewModel(macro.Id, loadFromDisk: false, macro, onMacroSaved);
        var editorWindow = new MacroEditorWindow(editorViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        editorWindow.Show();
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
        var recordWindow = new RecordWindow(recordViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        if (onClosed is not null)
            recordWindow.Closed += (_, _) => onClosed();
        recordWindow.Show();
    }

    private MacroEditorViewModel CreateEditorViewModel(
        MacroId macroId,
        bool loadFromDisk,
        Macro? inMemoryMacro,
        Action? onMacroSaved) =>
        new MacroEditorViewModel(
            serviceProvider.GetRequiredService<MacroWorkspaceService>(),
            serviceProvider.GetRequiredService<IUserDialogService>(),
            serviceProvider.GetRequiredService<IEditorInsertDialogs>(),
            serviceProvider.GetRequiredService<IPlaybackService>(),
            serviceProvider.GetRequiredService<RecordingCoordinator>(),
            serviceProvider.GetRequiredService<IUiLocalizer>(),
            macroId,
            loadFromDisk,
            inMemoryMacro,
            onMacroSaved);
}
