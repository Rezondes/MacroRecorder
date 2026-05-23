namespace MacroRecorder.Application.Ports;

public enum UpdatePromptChoice
{
    Later,
    OpenRelease
}

public interface IUpdatePromptModalHost
{
    UpdatePromptChoice ShowUpdateAvailable(UpdateCheckResult result);
}
