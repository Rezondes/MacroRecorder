using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

/// <summary>Host UI for in-window playback overlay (start delay + remaining time); optional.</summary>
public interface IPlaybackUiFeedback
{
    void Begin(Macro macro, int graceMs, TimeSpan estimatedPlayDuration);

    void UpdateStartDelayRemaining(TimeSpan remaining);

    void UpdatePlayingRemaining(TimeSpan remaining);

    void End();
}
