namespace MacroRecorder.Application.Ports;

public enum AppUpdateLaunchFailureReason
{
    None,
    UpdaterMissing,
    InstallDirectoryNotWritable,
    PortableZipUrlMissing,
    LaunchFailed
}

public sealed record AppUpdateLaunchResult(
    bool IsSuccess,
    AppUpdateLaunchFailureReason FailureReason = AppUpdateLaunchFailureReason.None);

public interface IAppUpdateService
{
    Task<AppUpdateLaunchResult> LaunchPortableUpdateAsync(
        UpdateCheckResult result,
        CancellationToken cancellationToken = default);
}
