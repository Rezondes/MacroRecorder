using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using MacroRecorder.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroRecorder.Infrastructure.Tests.Updates;

public sealed class PortableAppUpdateLauncherTests
{
    [Fact]
    public async Task LaunchPortableUpdateAsync_returns_missing_zip_url_reason()
    {
        var launcher = new PortableAppUpdateLauncher(NullLogger<PortableAppUpdateLauncher>.Instance);
        var result = new UpdateCheckResult(
            "0.0.1",
            "0.0.2",
            true,
            new Uri("https://example.com/release"),
            PortableZipDownloadUrl: null,
            ReleaseNotes: null);

        var launchResult = await launcher.LaunchPortableUpdateAsync(result);

        Assert.False(launchResult.IsSuccess);
        Assert.Equal(AppUpdateLaunchFailureReason.PortableZipUrlMissing, launchResult.FailureReason);
    }

    [Fact]
    public async Task LaunchPortableUpdateAsync_returns_updater_missing_when_process_path_has_no_updater()
    {
        var tempDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"update-launch-{Guid.NewGuid():N}")).FullName;
        try
        {
            var mainExePath = Path.Combine(tempDirectory, PortableAppUpdateLauncher.MainExecutableFileName);
            await File.WriteAllTextAsync(mainExePath, string.Empty);
            var launcher = new PortableAppUpdateLauncher(
                NullLogger<PortableAppUpdateLauncher>.Instance,
                () => mainExePath);
            var result = new UpdateCheckResult(
                "0.0.1",
                "0.0.2",
                true,
                new Uri("https://example.com/release"),
                new Uri("https://example.com/MacroRecorder-portable-win-x64-0.0.2.zip"),
                ReleaseNotes: null);

            var launchResult = await launcher.LaunchPortableUpdateAsync(result);

            Assert.False(launchResult.IsSuccess);
            Assert.Equal(AppUpdateLaunchFailureReason.UpdaterMissing, launchResult.FailureReason);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LaunchPortableUpdateAsync_returns_launch_failed_when_process_path_missing()
    {
        var launcher = new PortableAppUpdateLauncher(
            NullLogger<PortableAppUpdateLauncher>.Instance,
            () => null);
        var result = new UpdateCheckResult(
            "0.0.1",
            "0.0.2",
            true,
            new Uri("https://example.com/release"),
            new Uri("https://example.com/MacroRecorder-portable-win-x64-0.0.2.zip"),
            ReleaseNotes: null);

        var launchResult = await launcher.LaunchPortableUpdateAsync(result);

        Assert.False(launchResult.IsSuccess);
        Assert.Equal(AppUpdateLaunchFailureReason.LaunchFailed, launchResult.FailureReason);
    }
}
