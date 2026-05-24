using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace MacroRecorder.Updater;

internal static class Program
{
    private const int ParentWaitTimeoutSeconds = 120;
    private const string UpdatesFolderName = "updates";
    private const string LogFileName = "updater.log";

    public static async Task<int> Main(string[] args)
    {
        var logPath = Path.Combine(GetUpdatesRootDirectory(), LogFileName);
        try
        {
            if (!UpdaterArguments.TryParse(args, out var updaterArguments, out var parseError)
                || updaterArguments is null)
            {
                await WriteLogAsync(logPath, parseError ?? "Invalid arguments.").ConfigureAwait(false);
                return 1;
            }

            await WaitForParentProcessAsync(updaterArguments.WaitPid).ConfigureAwait(false);
            await RunUpdateAsync(updaterArguments, logPath).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            await WriteLogAsync(logPath, exception.ToString()).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task RunUpdateAsync(UpdaterArguments arguments, string logPath)
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
            await DownloadZipAsync(arguments.ZipUrl, zipPath).ConfigureAwait(false);
            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

            SyncInstallDirectory(
                arguments.InstallDirectory,
                extractDirectory,
                arguments.MainExe,
                arguments.UpdaterExe);

            StartMainApplication(arguments.InstallDirectory, arguments.MainExe);
        }
        finally
        {
            TryDeleteDirectory(extractDirectory);
            TryDeleteFile(zipPath);
        }
    }

    private static async Task DownloadZipAsync(string zipUrl, string zipPath)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MacroRecorderUpdater", "1.0"));
        using var response = await httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var fileStream = File.Create(zipPath);
        await responseStream.CopyToAsync(fileStream).ConfigureAwait(false);
    }

    private static void SyncInstallDirectory(
        string installDirectory,
        string extractDirectory,
        string mainExeFileName,
        string updaterExeFileName)
    {
        var preservedUpdaterPath = Path.Combine(installDirectory, updaterExeFileName);
        foreach (var existingFile in Directory.EnumerateFiles(installDirectory))
        {
            var fileName = Path.GetFileName(existingFile);
            if (string.Equals(fileName, updaterExeFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            File.Delete(existingFile);
        }

        foreach (var extractedFile in Directory.EnumerateFiles(extractDirectory))
        {
            var fileName = Path.GetFileName(extractedFile);
            if (string.Equals(fileName, updaterExeFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var destinationPath = Path.Combine(installDirectory, fileName);
            File.Copy(extractedFile, destinationPath, overwrite: true);
        }

        var extractedMainExe = Path.Combine(extractDirectory, mainExeFileName);
        if (!File.Exists(extractedMainExe))
            throw new InvalidOperationException($"Extracted release does not contain '{mainExeFileName}'.");

        if (!File.Exists(preservedUpdaterPath))
        {
            var extractedUpdater = Path.Combine(extractDirectory, updaterExeFileName);
            if (File.Exists(extractedUpdater))
                File.Copy(extractedUpdater, preservedUpdaterPath, overwrite: true);
        }
    }

    private static void StartMainApplication(string installDirectory, string mainExeFileName)
    {
        var mainExePath = Path.Combine(installDirectory, mainExeFileName);
        Process.Start(new ProcessStartInfo
        {
            FileName = mainExePath,
            WorkingDirectory = installDirectory,
            UseShellExecute = true
        });
    }

    private static async Task WaitForParentProcessAsync(int parentProcessId)
    {
        try
        {
            using var parentProcess = Process.GetProcessById(parentProcessId);
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(ParentWaitTimeoutSeconds));
            await parentProcess.WaitForExitAsync(cancellationSource.Token).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // Parent already exited.
        }
        catch (OperationCanceledException)
        {
            // Timed out waiting; continue with update.
        }
    }

    private static string GetUpdatesRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "MacroRecorderByRezondes", UpdatesFolderName);
    }

    private static async Task WriteLogAsync(string logPath, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.AppendAllTextAsync(
                logPath,
                $"[{DateTimeOffset.Now:u}] {message}{Environment.NewLine}").ConfigureAwait(false);
        }
        catch
        {
            // Best effort only.
        }
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
