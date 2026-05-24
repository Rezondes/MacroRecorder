using MacroRecorder.Logging;
using Serilog;
using Serilog.Events;

namespace MacroRecorder.Infrastructure.Tests.Logging;

public sealed class LoggingBootstrapTests : IDisposable
{
    private readonly string _logsDirectory = Path.Combine(Path.GetTempPath(), $"logs-bootstrap-{Guid.NewGuid():N}");

    [Fact]
    public async Task CreateFileLogger_writes_startup_line_to_override_directory()
    {
        var logger = LoggingBootstrap.CreateFileLogger(
            "test-bootstrap.log",
            LogEventLevel.Information,
            "test-version",
            _logsDirectory);
        logger.Information("bootstrap-test-line");
        if (logger is IDisposable disposableLogger)
            disposableLogger.Dispose();
        Log.CloseAndFlush();

        var logPath = Path.Combine(_logsDirectory, "test-bootstrap.log");
        Assert.True(File.Exists(logPath));
        var contents = await File.ReadAllTextAsync(logPath);
        Assert.Contains("bootstrap-test-line", contents);
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
        if (Directory.Exists(_logsDirectory))
            Directory.Delete(_logsDirectory, recursive: true);
    }
}
