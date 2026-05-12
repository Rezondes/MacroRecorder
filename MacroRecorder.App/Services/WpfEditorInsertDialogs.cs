using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

public sealed class WpfEditorInsertDialogs(
    Lazy<IEditorInsertModalHost> insertModalHost,
    Lazy<IEditEventModalHost> editEventModalHost) : IEditorInsertDialogs
{
    public bool? ShowMouseClickDialog(
        object ownerWindow,
        out int screenX,
        out int screenY,
        out MouseButtonKind mouseButton) =>
        insertModalHost.Value.ShowInsertMouseClickDialog(out screenX, out screenY, out mouseButton);

    public bool? ShowKeyStrokeDialog(
        object ownerWindow,
        out ushort virtualKey,
        out ushort scanCode,
        out bool isExtendedKey) =>
        insertModalHost.Value.ShowInsertKeyStrokeDialog(out virtualKey, out scanCode, out isExtendedKey);

    public bool? ShowRenameMacroDialog(object ownerWindow, string currentName, out string newMacroName) =>
        insertModalHost.Value.ShowRenameMacroDialog(currentName, out newMacroName);

    public bool? ShowEditSingleEventDialog(
        object ownerWindow,
        RecordedInputEvent currentEvent,
        out RecordedInputEvent updatedEvent) =>
        editEventModalHost.Value.ShowEditSingleEventDialog(currentEvent, out updatedEvent);
}
