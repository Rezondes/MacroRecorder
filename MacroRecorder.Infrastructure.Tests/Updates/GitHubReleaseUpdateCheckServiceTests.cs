using System.Net;
using System.Text;
using MacroRecorder.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroRecorder.Infrastructure.Tests.Updates;

public sealed class GitHubReleaseUpdateCheckServiceTests
{
    private const string SampleGitHubReleaseJson =
        """
        {
          "tag_name": "v0.0.2",
          "html_url": "https://github.com/Rezondes/MacroRecorder/releases/tag/v0.0.2",
          "body": "**Full Changelog**: https://github.com/Rezondes/MacroRecorder/commits/v0.0.2",
          "assets": [
            {
              "name": "MacroRecorder-portable-win-x64-0.0.2.zip",
              "browser_download_url": "https://github.com/Rezondes/MacroRecorder/releases/download/v0.0.2/MacroRecorder-portable-win-x64-0.0.2.zip"
            }
          ]
        }
        """;

    [Fact]
    public async Task CheckForUpdateAsync_parsesGitHubSnakeCaseReleaseJson()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleGitHubReleaseJson, Encoding.UTF8, "application/json")
        });
        var service = new GitHubReleaseUpdateCheckService(new HttpClient(handler), NullLogger<GitHubReleaseUpdateCheckService>.Instance);

        var result = await service.CheckForUpdateAsync();

        Assert.NotNull(result);
        Assert.Equal("0.0.2", result.LatestVersion);
        Assert.Equal(
            new Uri("https://github.com/Rezondes/MacroRecorder/releases/tag/v0.0.2"),
            result.ReleasePageUrl);
        Assert.Contains("Full Changelog", result.ReleaseNotes);
        Assert.False(string.IsNullOrWhiteSpace(result.CurrentVersion));
        Assert.Equal(
            new Uri("https://github.com/Rezondes/MacroRecorder/releases/download/v0.0.2/MacroRecorder-portable-win-x64-0.0.2.zip"),
            result.PortableZipDownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_whenPortableZipAssetMissing_leavesPortableZipDownloadUrlNull()
    {
        const string releaseWithoutZipAsset =
            """
            {
              "tag_name": "v0.0.2",
              "html_url": "https://github.com/Rezondes/MacroRecorder/releases/tag/v0.0.2",
              "body": "notes",
              "assets": [
                {
                  "name": "other.zip",
                  "browser_download_url": "https://example.com/other.zip"
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(releaseWithoutZipAsset, Encoding.UTF8, "application/json")
        });
        var service = new GitHubReleaseUpdateCheckService(new HttpClient(handler), NullLogger<GitHubReleaseUpdateCheckService>.Instance);

        var result = await service.CheckForUpdateAsync();

        Assert.NotNull(result);
        Assert.Null(result.PortableZipDownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_whenRemoteVersionIsNewer_reportsUpdateAvailable()
    {
        const string newerVersionJson =
            """
            {
              "tag_name": "v999.0.0",
              "html_url": "https://github.com/Rezondes/MacroRecorder/releases/tag/v999.0.0",
              "body": "future"
            }
            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(newerVersionJson, Encoding.UTF8, "application/json")
        });
        var service = new GitHubReleaseUpdateCheckService(new HttpClient(handler), NullLogger<GitHubReleaseUpdateCheckService>.Instance);

        var result = await service.CheckForUpdateAsync();

        Assert.NotNull(result);
        Assert.Equal("999.0.0", result.LatestVersion);
        Assert.True(result.IsUpdateAvailable);
    }
    [Fact]
    public async Task CheckForUpdateAsync_whenRemoteVersionIsOlder_reportsUpToDate()
    {
        const string olderVersionJson =
            """
            {
              "tag_name": "v0.0.0",
              "html_url": "https://github.com/Rezondes/MacroRecorder/releases/tag/v0.0.0",
              "body": "notes"
            }
            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(olderVersionJson, Encoding.UTF8, "application/json")
        });
        var service = new GitHubReleaseUpdateCheckService(new HttpClient(handler), NullLogger<GitHubReleaseUpdateCheckService>.Instance);

        var result = await service.CheckForUpdateAsync();

        Assert.NotNull(result);
        Assert.Equal("0.0.0", result.LatestVersion);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_whenGitHubReturnsNotFound_returnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = new GitHubReleaseUpdateCheckService(new HttpClient(handler), NullLogger<GitHubReleaseUpdateCheckService>.Instance);

        var result = await service.CheckForUpdateAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckForUpdateAsync_requestsLatestReleaseEndpoint()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleGitHubReleaseJson, Encoding.UTF8, "application/json")
            };
        });
        var service = new GitHubReleaseUpdateCheckService(new HttpClient(handler), NullLogger<GitHubReleaseUpdateCheckService>.Instance);

        _ = await service.CheckForUpdateAsync();

        Assert.Equal(
            new Uri("https://api.github.com/repos/Rezondes/MacroRecorder/releases/latest"),
            requestedUri);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
