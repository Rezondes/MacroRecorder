using MacroRecorder.Domain;
using MacroRecorder.Infrastructure.Interop;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.Infrastructure.Recording;

internal static class RecordingEnvironmentCapture
{
    public static RecordingEnvironment Capture(ILogger? logger = null)
    {
        var virtualScreenX = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var virtualScreenY = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var virtualScreenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var virtualScreenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        var primaryScreenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var primaryScreenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        uint systemDpi;
        try
        {
            systemDpi = NativeMethods.GetDpiForSystem();
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Failed to read system DPI; defaulting to 96");
            systemDpi = 96;
        }

        return new RecordingEnvironment(
            virtualScreenX,
            virtualScreenY,
            virtualScreenWidth,
            virtualScreenHeight,
            primaryScreenWidth,
            primaryScreenHeight,
            (int)systemDpi,
            (int)systemDpi,
            null);
    }
}
