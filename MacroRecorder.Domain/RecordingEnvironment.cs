namespace MacroRecorder.Domain;

public sealed record RecordingEnvironment(
    int VirtualScreenLeft,
    int VirtualScreenTop,
    int VirtualScreenWidth,
    int VirtualScreenHeight,
    int PrimaryScreenWidth,
    int PrimaryScreenHeight,
    int PrimaryDpiX,
    int PrimaryDpiY,
    int? OsBuild = null);
