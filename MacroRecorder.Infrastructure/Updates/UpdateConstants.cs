namespace MacroRecorder.Infrastructure.Updates;

internal static class UpdateConstants
{
    public const string GitHubRepo = "Rezondes/MacroRecorder";

    public static Uri LatestReleaseApiUri { get; } =
        new($"https://api.github.com/repos/{GitHubRepo}/releases/latest");
}
