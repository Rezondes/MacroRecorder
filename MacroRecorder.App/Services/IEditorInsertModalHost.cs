using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

/// <summary>Editor insert / rename UI hosted on the shell instead of separate <see cref="System.Windows.Window"/> instances.</summary>
public interface IEditorInsertModalHost
{
    bool? ShowInsertMouseClickDialog(out int screenX, out int screenY, out MouseButtonKind mouseButton);

    bool? ShowInsertKeyStrokeDialog(out ushort virtualKey, out ushort scanCode, out bool isExtendedKey);

    bool? ShowRenameMacroDialog(string currentName, out string newMacroName);
}
