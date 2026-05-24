using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;
using Microsoft.Extensions.Logging;

namespace MacroRecorder.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateCheckService(
    HttpClient httpClient,
    ILogger<GitHubReleaseUpdateCheckService> logger) : IUpdateCheckService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<UpdateCheckResult?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(UpdateConstants.LatestReleaseApiUri, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "GitHub release check failed with HTTP {StatusCode}",
                    (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName) || string.IsNullOrWhiteSpace(release.HtmlUrl))
                return null;

            var currentVersion = AppVersion.Current;
            var latestVersion = NormalizeTagVersion(release.TagName);
            if (string.IsNullOrWhiteSpace(latestVersion))
                return null;

            if (!Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var releaseUri))
                return null;

            var portableZipDownloadUrl = TryGetPortableZipDownloadUrl(release.Assets, latestVersion);
            var isUpdateAvailable = IsRemoteVersionNewer(latestVersion, currentVersion);
            logger.LogInformation(
                "Update check completed. Current {CurrentVersion}, latest {LatestVersion}, updateAvailable {UpdateAvailable}",
                currentVersion,
                latestVersion,
                isUpdateAvailable);
            return new UpdateCheckResult(
                currentVersion,
                latestVersion,
                isUpdateAvailable,
                releaseUri,
                portableZipDownloadUrl,
                release.Body);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "GitHub release check failed");
            return null;
        }
    }

    private static string NormalizeTagVersion(string tagName)
    {
        var trimmed = tagName.Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? trimmed[1..].Trim()
            : trimmed;
    }

    private static bool IsRemoteVersionNewer(string latestVersion, string currentVersion)
    {
        if (!Version.TryParse(NormalizeForVersionParse(latestVersion), out var latest))
            return false;
        if (!Version.TryParse(NormalizeForVersionParse(currentVersion), out var current))
            return string.CompareOrdinal(latestVersion, currentVersion) > 0;

        return latest > current;
    }

    private static Uri? TryGetPortableZipDownloadUrl(GitHubReleaseAssetResponse[]? assets, string latestVersion)
    {
        if (assets is null || assets.Length == 0)
            return null;

        var expectedName = PortableReleaseAssetNames.ZipFileName(latestVersion);
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Name) || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
                continue;
            if (!string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri)
                ? downloadUri
                : null;
        }

        return null;
    }

    private static string NormalizeForVersionParse(string version)
    {
        var core = version.Split('+', 2)[0];
        var parts = core.Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0.0",
            2 => $"{parts[0]}.{parts[1]}.0",
            _ => core
        };
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAssetResponse[]? Assets { get; set; }
    }

    private sealed class GitHubReleaseAssetResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
