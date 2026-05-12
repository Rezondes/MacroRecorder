using System.Windows;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

public sealed class WpfEditorInsertDialogs(ICursorPositionProvider cursor, IUiLocalizer loc) : IEditorInsertDialogs
{
    public bool? ShowMouseClickDialog(
        object ownerWindow,
        out int screenX,
        out int screenY,
        out MouseButtonKind mouseButton)
    {
        screenX = 0;
        screenY = 0;
        mouseButton = MouseButtonKind.Left;
        var dialog = new Views.Editor.InsertMouseClickDialog(cursor) { Owner = ownerWindow as Window };
        var showDialogResult = dialog.ShowDialog();
        if (showDialogResult != true)
            return showDialogResult;
        screenX = dialog.ScreenX;
        screenY = dialog.ScreenY;
        mouseButton = dialog.SelectedButton;
        return true;
    }

    public bool? ShowKeyStrokeDialog(
        object ownerWindow,
        out ushort virtualKey,
        out ushort scanCode,
        out bool isExtendedKey)
    {
        virtualKey = 0;
        scanCode = 0;
        isExtendedKey = false;
        var dialog = new Views.Editor.InsertKeyStrokeDialog(loc) { Owner = ownerWindow as Window };
        var showDialogResult = dialog.ShowDialog();
        if (showDialogResult != true)
            return showDialogResult;
        virtualKey = dialog.CapturedVk;
        scanCode = dialog.CapturedScan;
        isExtendedKey = dialog.CapturedExtended;
        return true;
    }

    public bool? ShowRenameMacroDialog(object ownerWindow, string currentName, out string newMacroName)
    {
        newMacroName = currentName;
        var dialog = new Views.Editor.RenameMacroDialog(loc, currentName) { Owner = ownerWindow as Window };
        var showDialogResult = dialog.ShowDialog();
        if (showDialogResult != true)
            return showDialogResult;
        newMacroName = dialog.NewName;
        return true;
    }

    public bool? ShowEditSingleEventDialog(
        object ownerWindow,
        RecordedInputEvent currentEvent,
        out RecordedInputEvent updatedEvent)
    {
        updatedEvent = currentEvent;
        var dialog = new Views.Editor.EditSingleEventDialog(currentEvent, loc) { Owner = ownerWindow as Window };
        var showDialogResult = dialog.ShowDialog();
        if (showDialogResult != true || dialog.ResultEvent is null)
            return showDialogResult;
        updatedEvent = dialog.ResultEvent with
        {
            ElapsedSinceSessionStart = currentEvent.ElapsedSinceSessionStart,
            Sequence = currentEvent.Sequence
        };
        return true;
    }
}
