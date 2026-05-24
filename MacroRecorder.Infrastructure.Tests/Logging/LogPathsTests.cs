using MacroRecorder.Logging;

namespace MacroRecorder.Infrastructure.Tests.Logging;

public sealed class LogPathsTests
{
    [Fact]
    public void LogsDirectory_is_under_local_app_data()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localAppData, LogPaths.LogsDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("MacroRecorderByRezondes", "logs"), LogPaths.LogsDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void App_and_updater_log_files_are_under_logs_directory()
    {
        Assert.Equal(Path.Combine(LogPaths.LogsDirectory, LogPaths.AppLogFileName), LogPaths.AppLogFile);
        Assert.Equal(Path.Combine(LogPaths.LogsDirectory, LogPaths.UpdaterLogFileName), LogPaths.UpdaterLogFile);
    }
}
