namespace MacroRecorder.Updater;

internal static class InstallDirectorySync
{
    public static SyncStats Sync(
        string installDirectory,
        string extractDirectory,
        string mainExeFileName,
        string updaterExeFileName)
    {
        var deletedCount = 0;
        var copiedCount = 0;
        var preservedUpdaterPath = Path.Combine(installDirectory, updaterExeFileName);
        foreach (var existingFile in Directory.EnumerateFiles(installDirectory))
        {
            var fileName = Path.GetFileName(existingFile);
            if (string.Equals(fileName, updaterExeFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            File.Delete(existingFile);
            deletedCount++;
        }

        foreach (var extractedFile in Directory.EnumerateFiles(extractDirectory))
        {
            var fileName = Path.GetFileName(extractedFile);
            if (string.Equals(fileName, updaterExeFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var destinationPath = Path.Combine(installDirectory, fileName);
            File.Copy(extractedFile, destinationPath, overwrite: true);
            copiedCount++;
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

        return new SyncStats(deletedCount, copiedCount);
    }

    internal readonly record struct SyncStats(int DeletedCount, int CopiedCount);
}
