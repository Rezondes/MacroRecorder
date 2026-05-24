namespace MacroRecorder.Infrastructure.Updates;

public static class PortableReleaseAssetNames
{
    public static string ZipFileName(string version) =>
        $"MacroRecorder-portable-win-x64-{version}.zip";
}
