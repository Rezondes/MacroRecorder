using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

public sealed class WpfEditorInsertDialogs(
    Lazy<IEditorInsertModalHost> insertModalHost,
    Lazy<IEditEventModalHost> editEventModalHost) : IEditorInsertDialogs
{
    public bool? ShowRenameMacroDialog(object ownerWindow, string currentName, out string newMacroName) =>
        insertModalHost.Value.ShowRenameMacroDialog(currentName, out newMacroName);

    public bool? ShowEditSingleEventDialog(
        object ownerWindow,
        RecordedInputEvent currentEvent,
        out RecordedInputEvent updatedEvent) =>
        editEventModalHost.Value.ShowEditSingleEventDialog(currentEvent, out updatedEvent);
}
