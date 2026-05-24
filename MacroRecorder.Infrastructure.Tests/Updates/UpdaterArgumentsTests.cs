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
}
