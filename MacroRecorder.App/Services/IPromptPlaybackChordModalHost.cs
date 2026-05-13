using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

public interface IPromptPlaybackChordModalHost
{
    /// <summary>Blocking modal: user presses a key combination or cancels. Returns <c>null</c> if cancelled.</summary>
    PlaybackKeyChord? PromptPlaybackChord(string title, string message, IReadOnlyList<PlaybackKeyChord> blockedChords);
}
