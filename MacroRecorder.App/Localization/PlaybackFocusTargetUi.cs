using MacroRecorder.Application.Ports;

namespace MacroRecorder.App.Localization;

internal static class PlaybackFocusTargetUi
{
    public static string FormatMessage(IUiLocalizer loc, PlaybackFocusTargetException exception) =>
        exception.Kind switch
        {
            PlaybackFocusTargetKind.AnchorMissing =>
                loc.GetString("Playback_FocusTarget_AnchorMissing"),
            PlaybackFocusTargetKind.WindowNotFound =>
                loc.GetString(
                    "Playback_FocusTarget_WindowNotFound",
                    exception.WindowTitle ?? "",
                    exception.ProcessName ?? ""),
            PlaybackFocusTargetKind.ClientSizeMismatch =>
                loc.GetString(
                    "Playback_FocusTarget_ClientSizeMismatch",
                    exception.ExpectedClientWidth ?? 0,
                    exception.ExpectedClientHeight ?? 0,
                    exception.ActualClientWidth ?? 0,
                    exception.ActualClientHeight ?? 0),
            _ => loc.GetString("Main_Play_ErrorPlaybackFailed")
        };
}
