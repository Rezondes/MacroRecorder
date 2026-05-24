namespace MacroRecorder.Application.Ports;

public enum UpdatePromptChoice
{
    Later,
    ApplyNow
}

public interface IUpdatePromptModalHost
{
    UpdatePromptChoice ShowUpdateAvailable(UpdateCheckResult result);
}
