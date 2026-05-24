using MacroRecorder.Updater;

namespace MacroRecorder.Infrastructure.Tests.Updates;

public sealed class UpdaterArgumentsTests
{
    [Fact]
    public void TryParse_parsesSeparatedOptionsWithoutClearingEarlierValues()
    {
        var args = new[]
        {
            "--wait-pid",
            "1234",
            "--zip-url",
            "https://github.com/Rezondes/MacroRecorder/releases/download/v0.0.5/MacroRecorder-portable-win-x64-0.0.5.zip",
            "--install-dir",
            Path.GetTempPath(),
            "--main-exe",
            "MacroRecorderByRezondes.exe",
            "--updater-exe",
            "MacroRecorderByRezondes.Updater.exe"
        };

        var parsed = UpdaterArguments.TryParse(args, out var arguments, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(arguments);
        Assert.Equal(1234, arguments.WaitPid);
        Assert.Contains("MacroRecorder-portable-win-x64-0.0.5.zip", arguments.ZipUrl);
        Assert.Equal("MacroRecorderByRezondes.exe", arguments.MainExe);
    }

    [Fact]
    public void TryParse_accepts_equals_syntax_for_options()
    {
        var installDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"updater-args-{Guid.NewGuid():N}")).FullName;
        var args = new[]
        {
            "--wait-pid=42",
            "--zip-url=https://example.com/MacroRecorder-portable-win-x64-0.0.2.zip",
            $"--install-dir={installDirectory}",
            "--main-exe=MacroRecorderByRezondes.exe",
            "--updater-exe=MacroRecorderByRezondes.Updater.exe"
        };

        var parsed = UpdaterArguments.TryParse(args, out var arguments, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(arguments);
        Assert.Equal(42, arguments.WaitPid);
        Assert.Equal(installDirectory, arguments.InstallDirectory);
    }

    [Fact]
    public void TryParse_rejects_unknown_argument()
    {
        var parsed = UpdaterArguments.TryParse(["positional"], out _, out var error);

        Assert.False(parsed);
        Assert.Contains("Unknown argument", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_reports_missing_zip_url()
    {
        var installDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"updater-args-{Guid.NewGuid():N}")).FullName;
        var args = new[]
        {
            "--wait-pid", "1",
            "--install-dir", installDirectory,
            "--main-exe", "MacroRecorderByRezondes.exe",
            "--updater-exe", "MacroRecorderByRezondes.Updater.exe"
        };

        var parsed = UpdaterArguments.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Contains("zip-url", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_reports_invalid_wait_pid()
    {
        var installDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"updater-args-{Guid.NewGuid():N}")).FullName;
        var args = new[]
        {
            "--wait-pid", "0",
            "--zip-url", "https://example.com/a.zip",
            "--install-dir", installDirectory,
            "--main-exe", "MacroRecorderByRezondes.exe",
            "--updater-exe", "MacroRecorderByRezondes.Updater.exe"
        };

        var parsed = UpdaterArguments.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Contains("wait-pid", error, StringComparison.OrdinalIgnoreCase);
    }
}
