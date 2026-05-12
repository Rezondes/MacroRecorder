using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IEditorInsertDialogs
{
    bool? ShowMouseClickDialog(
        object ownerWindow,
        out int screenX,
        out int screenY,
        out MouseButtonKind mouseButton);

    bool? ShowKeyStrokeDialog(
        object ownerWindow,
        out ushort virtualKey,
        out ushort scanCode,
        out bool isExtendedKey);

    bool? ShowRenameMacroDialog(object ownerWindow, string currentName, out string newMacroName);

    bool? ShowEditSingleEventDialog(
        object ownerWindow,
        RecordedInputEvent currentEvent,
        out RecordedInputEvent updatedEvent);
}
