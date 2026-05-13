namespace MacroRecorder.Application.Ports;

/// <summary>Windows <c>RegisterHotKey</c> failed for the current assignment set.</summary>
public sealed class PlaybackHotkeyRegistrationException : Exception
{
    public PlaybackHotkeyRegistrationException(string message, int win32Error)
        : base(message) =>
        Win32Error = win32Error;

    public int Win32Error { get; }
}
