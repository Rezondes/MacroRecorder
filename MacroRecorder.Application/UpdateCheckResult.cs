namespace MacroRecorder.Application;

public sealed record UpdateCheckResult(
    string CurrentVersion,
    string LatestVersion,
    bool IsUpdateAvailable,
    Uri ReleasePageUrl,
    string? ReleaseNotes);
