using System.Windows;
using System.Windows.Controls;

namespace MacroRecorder.Wpf.Controls;

/// <summary>
/// Application-wide tooltip timing and chrome (see implicit <c>ToolTip</c> style in <c>Themes/AppControls.xaml</c>).
/// Call <see cref="ConfigureToolTipService"/> once at startup (e.g. from <c>AppearanceService.Initialize</c>).
/// </summary>
public static class UiToolTip
{
    private static bool _configured;

    /// <summary>
    /// Sets snappier defaults than WPF's system tooltip delays and a reasonable show duration.
    /// Safe to call multiple times; metadata overrides apply only once per process.
    /// </summary>
    public static void ConfigureToolTipService()
    {
        if (_configured)
            return;
        _configured = true;

        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(450));
        ToolTipService.BetweenShowDelayProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(160));
        ToolTipService.ShowDurationProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(25_000));
    }
}
