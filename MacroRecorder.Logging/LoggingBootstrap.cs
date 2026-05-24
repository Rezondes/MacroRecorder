using Serilog;
using Serilog.Events;

namespace MacroRecorder.Logging;

/// <summary>Shared Serilog file logger configuration for app and updater.</summary>
public static class LoggingBootstrap
{
    private const long FileSizeLimitBytes = 5 * 1024 * 1024;
    private const int RetainedFileCountLimit = 5;

    public static Serilog.ILogger CreateFileLogger(string logFileName, LogEventLevel minLevel, string version)
    {
        Directory.CreateDirectory(LogPaths.LogsDirectory);
        var logPath = Path.Combine(LogPaths.LogsDirectory, logFileName);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .Enrich.WithProperty("Version", version)
            .Enrich.WithProperty("OS", Environment.OSVersion.VersionString)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Infinite,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: FileSizeLimitBytes,
                retainedFileCountLimit: RetainedFileCountLimit,
                shared: true)
            .CreateLogger();
    }
}
