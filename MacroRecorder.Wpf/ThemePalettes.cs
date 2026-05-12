using System.Windows.Media;

namespace MacroRecorder.Wpf;

internal static class ThemePalettes
{
    internal static ThemeColors Get(ThemeId themeId, bool isDark) =>
        (themeId, isDark) switch
        {
            (ThemeId.BlueGrey, false) => Light(PrimaryBlueGrey),
            (ThemeId.BlueGrey, true) => Dark(PrimaryBlueGrey),
            (ThemeId.Indigo, false) => Light(PrimaryIndigo),
            (ThemeId.Indigo, true) => Dark(PrimaryIndigo),
            (ThemeId.Teal, false) => Light(PrimaryTeal),
            (ThemeId.Teal, true) => Dark(PrimaryTeal),
            (ThemeId.DeepPurple, false) => Light(PrimaryDeepPurple),
            (ThemeId.DeepPurple, true) => Dark(PrimaryDeepPurple),
            (ThemeId.Amber, false) => LightAmber(),
            (ThemeId.Amber, true) => DarkAmber(),
            _ => Light(PrimaryBlueGrey)
        };

    private static readonly Color PrimaryBlueGrey = H("607D8B");
    private static readonly Color PrimaryIndigo = H("3F51B5");
    private static readonly Color PrimaryTeal = H("009688");
    private static readonly Color PrimaryDeepPurple = H("673AB7");
    private static readonly Color PrimaryAmber = H("FFC107");

    private static Color H(string hex) => (Color)ColorConverter.ConvertFromString("#" + hex)!;

    private static ThemeColors Light(Color primary)
    {
        var primaryDark = Darken(primary, 0.85f);
        return new ThemeColors(
            H("F5F5F5"),
            H("FFFFFF"),
            H("FAFAFA"),
            H("E0E0E0"),
            H("212121"),
            H("616161"),
            primary,
            primaryDark,
            H("FFFFFF"),
            Darken(primary, 0.92f),
            H("FFFFFF"),
            H("B00020"),
            H("FFFFFF"));
    }

    private static ThemeColors Dark(Color primary)
    {
        var onPrimary = Luminance(primary) > 0.55 ? H("212121") : H("FFFFFF");
        return new ThemeColors(
            H("121212"),
            H("1E1E1E"),
            H("2D2D2D"),
            H("424242"),
            H("EEEEEE"),
            H("B0B0B0"),
            primary,
            Darken(primary, 0.75f),
            onPrimary,
            Darken(primary, 0.88f),
            onPrimary,
            H("CF6679"),
            H("121212"));
    }

    private static ThemeColors LightAmber() =>
        new ThemeColors(
            H("F5F5F5"),
            H("FFFFFF"),
            H("FAFAFA"),
            H("E0E0E0"),
            H("212121"),
            H("616161"),
            PrimaryAmber,
            H("FFA000"),
            H("212121"),
            H("FFD54F"),
            H("212121"),
            H("B00020"),
            H("FFFFFF"));

    private static ThemeColors DarkAmber() =>
        new ThemeColors(
            H("121212"),
            H("1E1E1E"),
            H("2D2D2D"),
            H("424242"),
            H("EEEEEE"),
            H("B0B0B0"),
            PrimaryAmber,
            H("FFA000"),
            H("212121"),
            H("FFD54F"),
            H("212121"),
            H("CF6679"),
            H("121212"));

    private static Color Darken(Color c, float factor)
    {
        var r = (byte)(c.R * factor);
        var g = (byte)(c.G * factor);
        var b = (byte)(c.B * factor);
        return Color.FromRgb(r, g, b);
    }

    private static double Luminance(Color c)
    {
        static double Lin(byte u)
        {
            var x = u / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }
}
