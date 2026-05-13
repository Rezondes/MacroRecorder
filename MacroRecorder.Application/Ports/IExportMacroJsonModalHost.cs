namespace MacroRecorder.Application.Ports;

/// <summary>
/// Host for the shared macro JSON preview/export modal (UI thread).
/// </summary>
public interface IExportMacroJsonModalHost
{
    /// <param name="macroNameForFileDialog">Base name for the save dialog (extension added by the view).</param>
    /// <param name="json">Serialized macro JSON.</param>
    void ShowExportJsonModal(string macroNameForFileDialog, string json);
}
