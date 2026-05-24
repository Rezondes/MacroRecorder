using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using MacroRecorder.Logging;
using Serilog;
using Serilog.Events;

namespace MacroRecorder.Updater;

internal static class Program
{
    private const int ParentWaitTimeoutSeconds = 120;
    private const string UpdatesFolderName = "updates";

    public static async Task<int> Main(string[] args)
    {
        Log.Logger = LoggingBootstrap.CreateFileLogger(LogPaths.UpdaterLogFileName, LogEventLevel.Information, "1.0");
        var logger = Log.ForContext("Component", "Updater");

        UpdaterArguments? updaterArguments = null;
        try
        {
            logger.Information("ParseArgs: received {ArgumentCount} argument(s)", args.Length);

            if (!UpdaterArguments.TryParse(args, out updaterArguments, out var parseError)
                || updaterArguments is null)
            {
                logger.Error("ParseArgs failed: {ParseError}", parseError ?? "Invalid arguments.");
                return 1;
            }

            logger.Information(
                "ParseArgs succeeded. WaitPid {WaitPid}, ZipHost {ZipHost}, ZipFile {ZipFile}, InstallDirectory {InstallDirectory}",
                updaterArguments.WaitPid,
                SafeZipHost(updaterArguments.ZipUrl),
                SafeZipFileName(updaterArguments.ZipUrl),
                updaterArguments.InstallDirectory);

            await WaitForParentProcessAsync(updaterArguments.WaitPid, logger).ConfigureAwait(false);
            await RunUpdateAsync(updaterArguments, logger).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            logger.Fatal(exception, "Failed");
            if (updaterArguments is not null)
                TryStartMainApplication(updaterArguments.InstallDirectory, updaterArguments.MainExe);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task RunUpdateAsync(UpdaterArguments arguments, Serilog.ILogger logger)
    {
        var updatesRoot = GetUpdatesRootDirectory();
        Directory.CreateDirectory(updatesRoot);

        var zipFileName = Path.GetFileName(new Uri(arguments.ZipUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(zipFileName))
            zipFileName = "MacroRecorder-portable.zip";

        var zipPath = Path.Combine(updatesRoot, zipFileName);
        var extractDirectory = Path.Combine(updatesRoot, $"extract-{Guid.NewGuid():N}");

        try
        {
            await DownloadZipAsync(arguments.ZipUrl, zipPath, logger).ConfigureAwait(false);
            logger.Information("Extract: target directory {ExtractDirectory}", extractDirectory);
            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

            var syncStats = InstallDirectorySync.Sync(
                arguments.InstallDirectory,
                extractDirectory,
                arguments.MainExe,
                arguments.UpdaterExe);
            logger.Information(
                "Sync: deleted {DeletedCount} file(s), copied {CopiedCount} file(s) into {InstallDirectory}",
                syncStats.DeletedCount,
                syncStats.CopiedCount,
                arguments.InstallDirectory);

            var mainExePath = Path.Combine(arguments.InstallDirectory, arguments.MainExe);
            logger.Information("Restart: main executable {MainExePath}", mainExePath);
            StartMainApplication(arguments.InstallDirectory, arguments.MainExe);
        }
        finally
        {
            TryDeleteDirectory(extractDirectory);
            TryDeleteFile(zipPath);
        }
    }

    private static async Task DownloadZipAsync(string zipUrl, string zipPath, Serilog.ILogger logger)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MacroRecorderUpdater", "1.0"));
        using var response = await httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        logger.Information("Download: HTTP {StatusCode}", (int)response.StatusCode);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var fileStream = File.Create(zipPath);
        await responseStream.CopyToAsync(fileStream).ConfigureAwait(false);
        logger.Information("Download: wrote {ByteCount} byte(s) to {ZipPath}", fileStream.Length, zipPath);
    }

    private static void StartMainApplication(string installDirectory, string mainExeFileName) =>
        TryStartMainApplication(installDirectory, mainExeFileName);

    private static void TryStartMainApplication(string installDirectory, string mainExeFileName)
    {
        var mainExePath = Path.Combine(installDirectory, mainExeFileName);
        if (!File.Exists(mainExePath))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = mainExePath,
            WorkingDirectory = installDirectory,
            UseShellExecute = true
        });
    }

    private static async Task WaitForParentProcessAsync(int parentProcessId, Serilog.ILogger logger)
    {
        logger.Information("WaitParent: pid {ParentProcessId}, timeout {TimeoutSeconds}s", parentProcessId, ParentWaitTimeoutSeconds);
        try
        {
            using var parentProcess = Process.GetProcessById(parentProcessId);
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(ParentWaitTimeoutSeconds));
            await parentProcess.WaitForExitAsync(cancellationSource.Token).ConfigureAwait(false);
            logger.Information("WaitParent: parent process exited");
        }
        catch (ArgumentException)
        {
            logger.Information("WaitParent: parent process already exited");
        }
        catch (OperationCanceledException)
        {
            logger.Information("WaitParent: timed out waiting for parent process");
        }
    }

    private static string GetUpdatesRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "MacroRecorderByRezondes", UpdatesFolderName);
    }

    private static string SafeZipHost(string zipUrl)
    {
        if (!Uri.TryCreate(zipUrl, UriKind.Absolute, out var uri))
            return "unknown";
        return uri.Host;
    }

    private static string SafeZipFileName(string zipUrl)
    {
        if (!Uri.TryCreate(zipUrl, UriKind.Absolute, out var uri))
            return "unknown";
        return Path.GetFileName(uri.LocalPath);
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort only.
        }
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort only.
        }
    }

}
