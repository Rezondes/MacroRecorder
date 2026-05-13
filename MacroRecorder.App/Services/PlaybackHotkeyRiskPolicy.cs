using MacroRecorder.Domain;

namespace MacroRecorder.App.Services;

internal static class PlaybackHotkeyRiskPolicy
{
    private const uint VkC = 0x43;
    private const uint VkV = 0x56;
    private const uint VkX = 0x58;
    private const uint VkZ = 0x5A;
    private const uint VkA = 0x41;

    internal static bool IsRisky(PlaybackKeyChord chord)
    {
        if (!chord.Ctrl || chord.Alt || chord.Win)
            return false;
        return chord.VirtualKey is VkC or VkV or VkX or VkZ or VkA;
    }
}
