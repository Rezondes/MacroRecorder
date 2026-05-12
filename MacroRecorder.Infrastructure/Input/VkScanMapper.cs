using MacroRecorder.Infrastructure.Interop;

namespace MacroRecorder.Infrastructure.Input;

public static class VkScanMapper
{
    public static ushort VirtualKeyToScanCode(ushort virtualKey) =>
        (ushort)(NativeMethods.MapVirtualKey(virtualKey, NativeMethods.MAPVK_VK_TO_VSC) & 0xFFu);
}
