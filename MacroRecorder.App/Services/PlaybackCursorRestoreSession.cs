using System.Windows;
using System.Windows.Controls;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.App.Services;

/// <summary>Remembers the screen position of the last Play click and restores the cursor after playback ends.</summary>
public static class PlaybackCursorRestoreSession
{
    private static bool _armed;
    private static int _screenX;
    private static int _screenY;

    public static void ArmFromButton(Button? button)
    {
        if (button is null)
            return;

        button.UpdateLayout();
        var center = new Point(button.ActualWidth * 0.5, button.ActualHeight * 0.5);
        if (button.ActualWidth <= 0 || button.ActualHeight <= 0)
            center = new Point(8, 8);

        var screen = button.PointToScreen(center);
        _screenX = (int)Math.Round(screen.X);
        _screenY = (int)Math.Round(screen.Y);
        _armed = true;
    }

    public static void TryRestoreAndClear()
    {
        if (!_armed)
            return;
        _armed = false;
        CursorInterop.SetScreenPosition(_screenX, _screenY);
    }

    /// <summary>Clears a pending restore without moving the cursor (e.g. user cancelled playback from the overlay).</summary>
    public static void Disarm()
    {
        _armed = false;
    }
}
