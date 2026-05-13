using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IEditorInsertDialogs
{
    bool? ShowRenameMacroDialog(object ownerWindow, string currentName, out string newMacroName);

    bool? ShowEditSingleEventDialog(
        object ownerWindow,
        RecordedInputEvent currentEvent,
        out RecordedInputEvent updatedEvent);
}
