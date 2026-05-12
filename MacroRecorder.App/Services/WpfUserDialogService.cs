using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Services;

/// <summary>All prompts are hosted on the shell (see UI_HOSTING.txt).</summary>
public sealed class WpfUserDialogService(
    IUiLocalizer loc,
    Lazy<IUnsavedChangesModalHost> unsavedChangesModalHost,
    Lazy<IConfirmModalHost> confirmModalHost,
    Lazy<IPromptTextModalHost> promptTextModalHost,
    InAppInfoMessageChannel inAppInfo) : IUserDialogService
{
    public string? PromptText(
        string title,
        string message,
        string defaultValue = "",
        PromptTextValidator? validator = null,
        bool restrictInputToDigits = false) =>
        promptTextModalHost.Value.PromptText(title, message, defaultValue, validator, restrictInputToDigits);

    public void ShowInfo(string message) =>
        inAppInfo.RequestInfo(message, loc.GetString("Common_AppTitle"));

    public bool Confirm(string message) =>
        confirmModalHost.Value.Confirm(message, loc.GetString("Common_AppTitle"));

    public UnsavedChangesPromptResult PromptUnsavedChanges(
        string message,
        string title,
        UnsavedChangesPromptContext context = UnsavedChangesPromptContext.Editor) =>
        unsavedChangesModalHost.Value.ShowUnsavedChangesPrompt(message, title, context);
}
