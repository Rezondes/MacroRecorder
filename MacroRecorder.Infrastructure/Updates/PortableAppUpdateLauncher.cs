using System.Diagnostics;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.Infrastructure.Updates;

public sealed class PortableAppUpdateLauncher(ILogger<PortableAppUpdateLauncher> logger) : IAppUpdateService
{
    public const string MainExecutableFileName = "MacroRecorderByRezondes.exe";
    public const string UpdaterExecutableFileName = "MacroRecorderByRezondes.Updater.exe";

    public Task<AppUpdateLaunchResult> LaunchPortableUpdateAsync(
        UpdateCheckResult result,
        CancellationToken cancellationToken = default)
    {
        if (result.PortableZipDownloadUrl is null)
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.PortableZipUrlMissing));

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.LaunchFailed));

        var installDirectory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(installDirectory))
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.LaunchFailed));

        var updaterPath = Path.Combine(installDirectory, UpdaterExecutableFileName);
        if (!File.Exists(updaterPath))
        {
            logger.LogError("Portable update failed: updater missing at {UpdaterPath}", updaterPath);
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.UpdaterMissing));
        }

        if (!IsDirectoryWritable(installDirectory))
        {
            logger.LogError("Portable update failed: install directory not writable at {InstallDirectory}", installDirectory);
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.InstallDirectoryNotWritable));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var zipUri = result.PortableZipDownloadUrl;
            logger.LogInformation(
                "Launching portable updater. WaitPid {WaitPid}, ZipHost {ZipHost}, ZipFile {ZipFile}, InstallDirectory {InstallDirectory}",
                Environment.ProcessId,
                zipUri.Host,
                Path.GetFileName(zipUri.LocalPath),
                installDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                WorkingDirectory = installDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--wait-pid");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add("--zip-url");
            startInfo.ArgumentList.Add(result.PortableZipDownloadUrl.AbsoluteUri);
            startInfo.ArgumentList.Add("--install-dir");
            startInfo.ArgumentList.Add(installDirectory);
            startInfo.ArgumentList.Add("--main-exe");
            startInfo.ArgumentList.Add(MainExecutableFileName);
            startInfo.ArgumentList.Add("--updater-exe");
            startInfo.ArgumentList.Add(UpdaterExecutableFileName);

            var startedProcess = Process.Start(startInfo);
            if (startedProcess is null)
            {
                logger.LogError("Portable update failed: Process.Start returned null for updater");
                return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.LaunchFailed));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Portable update failed to start updater");
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.LaunchFailed));
        }

        return Task.FromResult(new AppUpdateLaunchResult(true));
    }

    private static bool IsDirectoryWritable(string directoryPath)
    {
        var probePath = Path.Combine(directoryPath, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
