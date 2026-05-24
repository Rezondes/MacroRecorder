using System.Diagnostics;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.Infrastructure.Updates;

public sealed class PortableAppUpdateLauncher : IAppUpdateService
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
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.UpdaterMissing));

        if (!IsDirectoryWritable(installDirectory))
            return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.InstallDirectoryNotWritable));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
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
                return Task.FromResult(new AppUpdateLaunchResult(false, AppUpdateLaunchFailureReason.LaunchFailed));
        }
        catch
        {
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
