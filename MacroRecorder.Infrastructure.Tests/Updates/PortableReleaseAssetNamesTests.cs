using MacroRecorder.Infrastructure.Updates;

namespace MacroRecorder.Infrastructure.Tests.Updates;

public sealed class PortableReleaseAssetNamesTests
{
    [Theory]
    [InlineData("0.0.6", "MacroRecorder-portable-win-x64-0.0.6.zip")]
    [InlineData("1.2.3", "MacroRecorder-portable-win-x64-1.2.3.zip")]
    public void ZipFileName_formats_expected_asset_name(string version, string expected)
    {
        Assert.Equal(expected, PortableReleaseAssetNames.ZipFileName(version));
    }
}
