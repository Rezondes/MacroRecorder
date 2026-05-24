namespace MacroRecorder.Logging;

/// <summary>
/// Privacy rules for structured logging across MacroRecorder.
/// Never log macro JSON, window titles, keyboard/mouse event payloads, or file contents.
/// </summary>
public static class LoggingPrivacy
{
    /// <summary>Safe to log: exception types/messages, HTTP status codes, byte counts, file paths, phase names, version strings.</summary>
    public static bool IsSafeDiagnosticField(string fieldName) =>
        fieldName is "Version" or "ProcessId" or "StatusCode" or "ByteCount" or "Phase" or "FilePath";

    /// <summary>Never include in log messages or structured properties.</summary>
    public static readonly string[] ForbiddenContent =
    [
        "macro json",
        "window title",
        "keyboard event",
        "mouse event",
        "recorded input",
        "file contents"
    ];
}
