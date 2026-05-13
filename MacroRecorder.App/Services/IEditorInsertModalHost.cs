using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

/// <summary>Editor insert / rename UI hosted on the shell instead of separate <see cref="System.Windows.Window"/> instances.</summary>
public interface IEditorInsertModalHost
{
    bool? ShowRenameMacroDialog(string currentName, out string newMacroName);
}
