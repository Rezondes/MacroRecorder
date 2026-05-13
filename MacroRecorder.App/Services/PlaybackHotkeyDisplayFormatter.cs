using MacroRecorder.Application.Ports;
using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

internal static class PlaybackHotkeyDisplayFormatter
{
    internal static string Format(PlaybackKeyChord chord, IUiLocalizer loc)
    {
        var parts = new List<string>(5);
        if (chord.Ctrl)
            parts.Add(loc.GetString("Hotkey_Modifier_Ctrl"));
        if (chord.Shift)
            parts.Add(loc.GetString("Hotkey_Modifier_Shift"));
        if (chord.Alt)
            parts.Add(loc.GetString("Hotkey_Modifier_Alt"));
        if (chord.Win)
            parts.Add(loc.GetString("Hotkey_Modifier_Win"));

        parts.Add(FormatVirtualKey(chord.VirtualKey));
        return string.Join(loc.GetString("Hotkey_ChordSeparator"), parts);
    }

    private static string FormatVirtualKey(uint vk)
    {
        if (vk >= 0x30 && vk <= 0x39)
            return ((char)vk).ToString();
        if (vk >= 0x41 && vk <= 0x5A)
            return ((char)vk).ToString();
        if (vk >= 0x70 && vk <= 0x87)
            return "F" + (vk - 0x6F);

        return vk switch
        {
            0x08 => "Back",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            _ => "0x" + vk.ToString("X")
        };
    }
}
