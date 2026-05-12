using MacroRecorder.Domain;

namespace MacroRecorder.Application.Ports;

public interface IPlaybackService
{
    Task PlayAsync(Macro macro, CancellationToken cancellationToken = default);
}
