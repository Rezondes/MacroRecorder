namespace MacroRecorder.Logging;

/// <summary>Resolved paths for rolling log files under LocalAppData.</summary>
public static class LogPaths
{
    private const string AppDataFolderName = "MacroRecorderByRezondes";
    private const string LogsFolderName = "logs";

    public const string AppLogFileName = "app.log";
    public const string UpdaterLogFileName = "updater.log";

    /// <summary>%LocalAppData%\MacroRecorderByRezondes\logs\</summary>
    public static string LogsDirectory { get; } = BuildLogsDirectory();

    public static string AppLogFile => Path.Combine(LogsDirectory, AppLogFileName);

    public static string UpdaterLogFile => Path.Combine(LogsDirectory, UpdaterLogFileName);

    private static string BuildLogsDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppDataFolderName, LogsFolderName);
    }
}
