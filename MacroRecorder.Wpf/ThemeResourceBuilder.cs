using System.Windows;
using System.Windows.Media;

namespace MacroRecorder.Wpf;

public static class ThemeResourceBuilder
{
    public static ResourceDictionary Build(ThemeId themeId, bool isDark)
    {
        var c = ThemePalettes.Get(themeId, isDark);
        var dict = new ResourceDictionary();
        Add(dict, "UiBrush.WindowBackground", c.WindowBackground);
        Add(dict, "UiBrush.Surface", c.Surface);
        Add(dict, "UiBrush.SurfaceElevated", c.SurfaceElevated);
        Add(dict, "UiBrush.Border", c.Border);
        Add(dict, "UiBrush.TextPrimary", c.TextPrimary);
        Add(dict, "UiBrush.TextSecondary", c.TextSecondary);
        Add(dict, "UiBrush.Primary", c.Primary);
        Add(dict, "UiBrush.PrimaryDark", c.PrimaryDark);
        Add(dict, "UiBrush.OnPrimary", c.OnPrimary);
        Add(dict, "UiBrush.Secondary", c.Secondary);
        Add(dict, "UiBrush.OnSecondary", c.OnSecondary);
        Add(dict, "UiBrush.Error", c.Error);
        Add(dict, "UiBrush.OnError", c.OnError);
        Add(dict, "UiBrush.PrimaryHover", Lerp(c.Primary, Color.FromRgb(255, 255, 255), isDark ? 0.14 : 0.12));
        Add(dict, "UiBrush.OverlayScrim", Color.FromArgb(0x99, 0, 0, 0));
        Add(dict, "UiBrush.SurfaceHover", Lerp(c.Surface, c.Primary, isDark ? 0.12 : 0.06));
        Add(dict, "UiBrush.SurfaceSelected", Lerp(c.Surface, c.Primary, isDark ? 0.22 : 0.14));
        var playbackTint = Lerp(c.Error, c.Primary, isDark ? 0.38 : 0.28);
        Add(dict, "UiBrush.PlaybackOverlay", Color.FromArgb(0xE4, playbackTint.R, playbackTint.G, playbackTint.B));
        Add(dict, "UiBrush.PlaybackOverlayText", c.OnError);
        return dict;
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static void Add(ResourceDictionary dict, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dict[key] = brush;
    }

    public static ResourceDictionary LoadAppControls()
    {
        return new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/MacroRecorder.Wpf;component/Themes/AppControls.xaml",
                UriKind.Absolute)
        };
    }
}
