using MacroRecorder.Updater;

namespace MacroRecorder.Infrastructure.Tests.Updates;

public sealed class InstallDirectorySyncTests : IDisposable
{
    private readonly string _installDirectory;
    private readonly string _extractDirectory;
    private const string MainExe = "MacroRecorderByRezondes.exe";
    private const string UpdaterExe = "MacroRecorderByRezondes.Updater.exe";

    public InstallDirectorySyncTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"install-sync-{Guid.NewGuid():N}");
        _installDirectory = Path.Combine(root, "install");
        _extractDirectory = Path.Combine(root, "extract");
        Directory.CreateDirectory(_installDirectory);
        Directory.CreateDirectory(_extractDirectory);
    }

    [Fact]
    public void Sync_replaces_main_exe_and_preserves_existing_updater()
    {
        var installUpdaterPath = Path.Combine(_installDirectory, UpdaterExe);
        var installMainPath = Path.Combine(_installDirectory, MainExe);
        File.WriteAllText(installUpdaterPath, "old-updater");
        File.WriteAllText(installMainPath, "old-main");
        File.WriteAllText(Path.Combine(_installDirectory, "legacy.dll"), "legacy");
        File.WriteAllText(Path.Combine(_extractDirectory, MainExe), "new-main");
        File.WriteAllText(Path.Combine(_extractDirectory, UpdaterExe), "new-updater-should-not-replace");
        File.WriteAllText(Path.Combine(_extractDirectory, "new.dll"), "new");

        var stats = InstallDirectorySync.Sync(_installDirectory, _extractDirectory, MainExe, UpdaterExe);

        Assert.Equal("new-main", File.ReadAllText(installMainPath));
        Assert.Equal("old-updater", File.ReadAllText(installUpdaterPath));
        Assert.False(File.Exists(Path.Combine(_installDirectory, "legacy.dll")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(_installDirectory, "new.dll")));
        Assert.Equal(2, stats.DeletedCount);
        Assert.Equal(2, stats.CopiedCount);
    }

    [Fact]
    public void Sync_copies_updater_when_install_folder_has_none()
    {
        File.WriteAllText(Path.Combine(_extractDirectory, MainExe), "new-main");
        File.WriteAllText(Path.Combine(_extractDirectory, UpdaterExe), "new-updater");

        InstallDirectorySync.Sync(_installDirectory, _extractDirectory, MainExe, UpdaterExe);

        Assert.Equal("new-updater", File.ReadAllText(Path.Combine(_installDirectory, UpdaterExe)));
    }

    [Fact]
    public void Sync_throws_when_extracted_release_lacks_main_exe()
    {
        File.WriteAllText(Path.Combine(_extractDirectory, UpdaterExe), "updater");

        Assert.Throws<InvalidOperationException>(() =>
            InstallDirectorySync.Sync(_installDirectory, _extractDirectory, MainExe, UpdaterExe));
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_installDirectory)!.FullName;
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
