using System.Text;
using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.Infrastructure.Input;

/// <summary>
/// Resolves keyboard events to a human-readable key label via <c>GetKeyNameText</c> (current keyboard layout).
/// Virtual key and scan code in domain events are unchanged; this is display-only.
/// </summary>
public static class KeyDisplayName
{
    private const int BufferChars = 64;

    /// <summary>Returns a non-empty display name, or <c>VK n</c> if Windows cannot resolve the key.</summary>
    public static string GetName(ushort virtualKey, ushort scanCode, bool isExtendedKey)
    {
        var fromStored = TryGetName(scanCode, isExtendedKey);
        if (!string.IsNullOrEmpty(fromStored))
            return fromStored;

        var mapped = VkScanMapper.VirtualKeyToScanCode(virtualKey);
        if (mapped != 0)
        {
            var fromMapped = TryGetName(mapped, isExtendedKey);
            if (!string.IsNullOrEmpty(fromMapped))
                return fromMapped;

            var toggled = TryGetName(mapped, !isExtendedKey);
            if (!string.IsNullOrEmpty(toggled))
                return toggled;
        }

        return $"VK {virtualKey}";
    }

    private static string? TryGetName(ushort scanCode, bool isExtendedKey)
    {
        var scan = scanCode & 0xFFu;
        if (scan == 0)
            return null;

        var lParam = (int)(scan << 16);
        if (isExtendedKey)
            lParam |= 1 << 24;

        var sb = new StringBuilder(BufferChars);
        var len = NativeMethods.GetKeyNameText(lParam, sb, BufferChars);
        if (len <= 0)
            return null;

        var s = sb.ToString().Trim();
        return s.Length > 0 ? s : null;
    }
}
