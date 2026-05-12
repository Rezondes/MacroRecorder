namespace MacroRecorder.App.Services;

/// <summary>Yes / No confirmation hosted on the main window (see UI_HOSTING.txt).</summary>
public interface IConfirmModalHost
{
    bool Confirm(string message, string title);
}
