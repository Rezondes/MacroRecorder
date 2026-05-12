using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Services;

/// <summary>Shows Save / Discard / Cancel for unsaved changes inside the main window.</summary>
public interface IUnsavedChangesModalHost
{
    UnsavedChangesPromptResult ShowUnsavedChangesPrompt(
        string message,
        string title,
        UnsavedChangesPromptContext context = UnsavedChangesPromptContext.Editor);
}
