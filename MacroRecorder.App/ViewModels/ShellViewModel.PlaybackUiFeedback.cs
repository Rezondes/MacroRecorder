using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroRecorder.App.Services;
using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace MacroRecorder.App.ViewModels;

public partial class ShellViewModel
{
    /// <summary>After the last injected input, keep overlay visible this long before dismiss + cursor restore.</summary>
    private static readonly TimeSpan PlaybackPostPlayTailDelay = TimeSpan.FromMilliseconds(500);

    private DispatcherTimer? _playbackPostPlayTailTimer;

    [ObservableProperty]
    private bool isPlaybackOverlayVisible;

    [ObservableProperty]
    private string playbackOverlayTitle = "";

    [ObservableProperty]
    private string playbackOverlayBody = "";

    [ObservableProperty]
    private string playbackOverlayCountdown = "";

    [ObservableProperty]
    private bool showPlaybackStartDelayCancel;

    void IPlaybackUiFeedback.Begin(Macro macro, int graceMs, TimeSpan estimatedPlayDuration)
    {
        _ = macro;
        _ = estimatedPlayDuration;
        RunOnUi(DispatcherPriority.Normal, () =>
        {
            CancelPlaybackPostPlayTailTimer();
            PlaybackOverlayTitle = _loc.GetString("Playback_Overlay_WarningTitle");
            PlaybackOverlayBody = _loc.GetString("Playback_Overlay_WarningBody");
            PlaybackOverlayCountdown = "";
            ShowPlaybackStartDelayCancel = graceMs > 0;
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
        {
            ShowPlaybackStartDelayCancel = false;
            PlaybackOverlayCountdown = _loc.GetString("Playback_Overlay_RemainingFormat", formatted);
        });
    }

    void IPlaybackUiFeedback.End()
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null)
            return;

        void SchedulePostPlayTail()
        {
            CancelPlaybackPostPlayTailTimer();
            ShowPlaybackStartDelayCancel = false;
            var timer = new DispatcherTimer
            {
                Interval = PlaybackPostPlayTailDelay
            };
            _playbackPostPlayTailTimer = timer;
            timer.Tick += OnPlaybackPostPlayTailTimerTick;
            timer.Start();
        }

        if (d.CheckAccess())
            SchedulePostPlayTail();
        else
            d.Invoke(SchedulePostPlayTail, DispatcherPriority.Normal);
    }

    private void CancelPlaybackPostPlayTailTimer()
    {
        if (_playbackPostPlayTailTimer is null)
            return;
        _playbackPostPlayTailTimer.Stop();
        _playbackPostPlayTailTimer.Tick -= OnPlaybackPostPlayTailTimerTick;
        _playbackPostPlayTailTimer = null;
    }

    private void OnPlaybackPostPlayTailTimerTick(object? sender, EventArgs e)
    {
        if (sender is not DispatcherTimer timer || !ReferenceEquals(timer, _playbackPostPlayTailTimer))
            return;
        CancelPlaybackPostPlayTailTimer();
        IsPlaybackOverlayVisible = false;
        PlaybackOverlayTitle = "";
        PlaybackOverlayBody = "";
        PlaybackOverlayCountdown = "";
        ShowPlaybackStartDelayCancel = false;
        PlaybackCursorRestoreSession.TryRestoreAndClear();
    }

    [RelayCommand]
    private void CancelPlaybackFromOverlay()
    {
        PlaybackCursorRestoreSession.Disarm();
        _services.GetRequiredService<IPlaybackService>().RequestUserCancel();
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
