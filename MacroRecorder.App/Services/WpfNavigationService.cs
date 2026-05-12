using System.Windows;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.App.Services;

public sealed class WpfNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public void OpenEditor(MacroId id, Action? onMacroSaved = null)
    {
        var editorViewModel = ActivatorUtilities.CreateInstance<MacroEditorViewModel>(
            serviceProvider,
            new object[] { id, true, null!, onMacroSaved! });
        var editorWindow = new MacroEditorWindow(editorViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        editorWindow.Show();
    }

    public void OpenNewMacroEditor(Macro macro, Action? onMacroSaved = null)
    {
        var editorViewModel = ActivatorUtilities.CreateInstance<MacroEditorViewModel>(
            serviceProvider,
            new object[] { macro.Id, false, macro, onMacroSaved! });
        var editorWindow = new MacroEditorWindow(editorViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        editorWindow.Show();
    }

    public void OpenRecording(Action? onClosed = null, Action? onMacroSavedFromEditor = null)
    {
        var recordViewModel = ActivatorUtilities.CreateInstance<RecordViewModel>(
            serviceProvider,
            new object[] { onMacroSavedFromEditor! });
        var recordWindow = new RecordWindow(recordViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        if (onClosed is not null)
            recordWindow.Closed += (_, _) => onClosed();
        recordWindow.Show();
    }
}
