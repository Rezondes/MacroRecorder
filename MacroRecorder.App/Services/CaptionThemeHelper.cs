using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MacroRecorder.App.Services;

/// <summary>Maps non-client (title bar) colors to the active theme via DWM on Windows 10/11.</summary>
internal static class CaptionThemeHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    /// <summary>Windows 11 21H2+ caption color attributes.</summary>
    private static bool SupportsCaptionColorAttributes =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, in int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, in uint pvAttribute, int cbAttribute);

    internal static void Apply(Window window, bool useDarkChrome)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var dark = useDarkChrome ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, in dark, Marshal.SizeOf<int>());

        if (!SupportsCaptionColorAttributes)
            return;

        if (window.TryFindResource("UiBrush.WindowBackground") is not SolidColorBrush windowBg)
            return;
        if (window.TryFindResource("UiBrush.TextPrimary") is not SolidColorBrush textPrimary)
            return;
        if (window.TryFindResource("UiBrush.Border") is not SolidColorBrush border)
            return;

        var caption = ToColorRef(windowBg.Color);
        var text = ToColorRef(textPrimary.Color);
        var borderRef = ToColorRef(border.Color);
        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, in caption, sizeof(uint));
        _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, in text, sizeof(uint));
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, in borderRef, sizeof(uint));
    }

    /// <summary>COLORREF 0x00BBGGRR</summary>
    private static uint ToColorRef(Color c) => (uint)(c.R | (c.G << 8) | (c.B << 16));
}
