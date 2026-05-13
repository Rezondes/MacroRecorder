using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.ViewModels;

public partial class ShellViewModel
{
    [ObservableProperty]
    private bool isPlaybackOverlayVisible;

    [ObservableProperty]
    private string playbackOverlayTitle = "";

    [ObservableProperty]
    private string playbackOverlayBody = "";

    [ObservableProperty]
    private string playbackOverlayCountdown = "";

    void IPlaybackUiFeedback.Begin(Macro macro, int graceMs, TimeSpan estimatedPlayDuration)
    {
        _ = macro;
        _ = estimatedPlayDuration;
        RunOnUi(DispatcherPriority.Normal, () =>
        {
            PlaybackOverlayTitle = _loc.GetString("Playback_Overlay_WarningTitle");
            PlaybackOverlayBody = _loc.GetString("Playback_Overlay_WarningBody");
            PlaybackOverlayCountdown = "";
            IsPlaybackOverlayVisible = true;
        });
    }

    void IPlaybackUiFeedback.UpdateStartDelayRemaining(TimeSpan remaining)
    {
        var formatted = FormatPlaybackCountdown(remaining);
        RunOnUi(DispatcherPriority.Normal, () =>
            PlaybackOverlayCountdown = _loc.GetString("Playback_Overlay_StartDelayFormat", formatted));
    }

    void IPlaybackUiFeedback.UpdatePlayingRemaining(TimeSpan remaining)
    {
        var formatted = FormatPlaybackCountdown(remaining);
        RunOnUi(DispatcherPriority.Normal, () =>
            PlaybackOverlayCountdown = _loc.GetString("Playback_Overlay_RemainingFormat", formatted));
    }

    void IPlaybackUiFeedback.End()
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null)
            return;

        void Clear()
        {
            IsPlaybackOverlayVisible = false;
            PlaybackOverlayTitle = "";
            PlaybackOverlayBody = "";
            PlaybackOverlayCountdown = "";
        }

        if (d.CheckAccess())
            Clear();
        else
            d.Invoke(Clear, DispatcherPriority.Normal);
    }

    private void RunOnUi(DispatcherPriority priority, Action action)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null)
            return;

        if (d.CheckAccess())
            action();
        else
            _ = d.BeginInvoke(priority, action);
    }

    private static string FormatPlaybackCountdown(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
            t = TimeSpan.Zero;

        var totalSec = (int)Math.Floor(t.TotalSeconds + 0.5);
        var m = totalSec / 60;
        var s = totalSec % 60;
        return m.ToString(CultureInfo.InvariantCulture) + ":" + s.ToString("D2", CultureInfo.InvariantCulture);
    }
}
