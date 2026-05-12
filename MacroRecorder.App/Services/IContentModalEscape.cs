namespace MacroRecorder.App.Services;

/// <summary>Lets the shell close the content modal on Escape (same pattern as <see cref="Views.Editor.EditSingleEventView"/>).</summary>
public interface IContentModalEscape
{
    void CancelFromHost();
}
