using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Services;

/// <summary>Single-line text prompt hosted on the shell (replaces <c>PromptText</c> window).</summary>
public interface IPromptTextModalHost
{
    string? PromptText(
        string title,
        string message,
        string defaultValue = "",
        PromptTextValidator? validator = null,
        bool restrictInputToDigits = false);
}
