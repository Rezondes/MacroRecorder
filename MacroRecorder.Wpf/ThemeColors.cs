using System.Windows.Media;

namespace MacroRecorder.Wpf;

public readonly struct ThemeColors(
    Color windowBackground,
    Color surface,
    Color surfaceElevated,
    Color border,
    Color textPrimary,
    Color textSecondary,
    Color primary,
    Color primaryDark,
    Color onPrimary,
    Color secondary,
    Color onSecondary,
    Color error,
    Color onError)
{
    public Color WindowBackground { get; } = windowBackground;
    public Color Surface { get; } = surface;
    public Color SurfaceElevated { get; } = surfaceElevated;
    public Color Border { get; } = border;
    public Color TextPrimary { get; } = textPrimary;
    public Color TextSecondary { get; } = textSecondary;
    public Color Primary { get; } = primary;
    public Color PrimaryDark { get; } = primaryDark;
    public Color OnPrimary { get; } = onPrimary;
    public Color Secondary { get; } = secondary;
    public Color OnSecondary { get; } = onSecondary;
    public Color Error { get; } = error;
    public Color OnError { get; } = onError;
}
