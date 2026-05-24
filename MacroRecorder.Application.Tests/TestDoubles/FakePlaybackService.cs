using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.Application.Tests.TestDoubles;

internal sealed class FakePlaybackService : IPlaybackService
{
    public List<MacroId> PlayedMacroIds { get; } = new();
    public int PlayCallCount { get; private set; }

    public Task PlayAsync(
        Macro macro,
        CancellationToken cancellationToken = default,
        int userInputInterruptGraceMilliseconds = 0,
        bool playbackFocusBringWindowToForeground = true,
        bool playbackFocusRestoreIfMinimized = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlayCallCount++;
        PlayedMacroIds.Add(macro.Id);
        return Task.CompletedTask;
    }

    public void RequestUserCancel()
    {
    }
}
