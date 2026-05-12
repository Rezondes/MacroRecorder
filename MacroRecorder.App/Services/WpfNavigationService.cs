using System.Windows;
using MacroRecorder.App.ViewModels;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.App.Services;

public sealed class WpfNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public void OpenEditor(MacroId id)
    {
        var editorViewModel = ActivatorUtilities.CreateInstance<MacroEditorViewModel>(
            serviceProvider,
            id,
            true,
            (Macro?)null);
        var editorWindow = new MacroEditorWindow(editorViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        editorWindow.Show();
    }

    public void OpenNewMacroEditor(Macro macro)
    {
        var editorViewModel = ActivatorUtilities.CreateInstance<MacroEditorViewModel>(
            serviceProvider,
            macro.Id,
            false,
            macro);
        var editorWindow = new MacroEditorWindow(editorViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        editorWindow.Show();
    }

    public void OpenRecording(Action? onClosed = null)
    {
        var recordViewModel = serviceProvider.GetRequiredService<RecordViewModel>();
        var recordWindow = new RecordWindow(recordViewModel) { Owner = System.Windows.Application.Current?.MainWindow };
        if (onClosed is not null)
            recordWindow.Closed += (_, _) => onClosed();
        recordWindow.Show();
    }
}
