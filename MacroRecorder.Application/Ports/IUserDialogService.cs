namespace MacroRecorder.Application.Ports;

/// <summary>Returns a user-visible error message to keep the prompt open; <c>null</c> accepts and closes with OK.</summary>
public delegate string? PromptTextValidator(string text);

public interface IUserDialogService
{
    string? PromptText(
        string title,
        string message,
        string defaultValue = "",
        PromptTextValidator? validator = null,
        bool restrictInputToDigits = false);

    void ShowInfo(string message);

    bool Confirm(string message);

    UnsavedChangesPromptResult PromptUnsavedChanges(
        string message,
        string title,
        UnsavedChangesPromptContext context = UnsavedChangesPromptContext.Editor);
}
