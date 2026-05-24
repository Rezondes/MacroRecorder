using System.Diagnostics;
using System.IO;
using MacroRecorder.Logging;

namespace MacroRecorder.App.Services;

public static class LogFolderOpener
{
    public static void OpenLogsDirectory()
    {
        Directory.CreateDirectory(LogPaths.LogsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = LogPaths.LogsDirectory,
            UseShellExecute = true
        });
    }
}
