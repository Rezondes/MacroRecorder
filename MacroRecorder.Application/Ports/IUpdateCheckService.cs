using MacroRecorder.Application;

namespace MacroRecorder.Application.Ports;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
