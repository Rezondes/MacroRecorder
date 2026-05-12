namespace MacroRecorder.Application.Ports;

public interface IUserDialogService
{
    string? PromptText(string title, string message, string defaultValue = "");

    void ShowInfo(string message);

    bool Confirm(string message);

    UnsavedChangesPromptResult PromptUnsavedChanges(string message, string title);
}
