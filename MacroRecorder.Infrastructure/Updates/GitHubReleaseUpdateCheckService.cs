using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacroRecorder.Application;
using MacroRecorder.Application.Ports;

namespace MacroRecorder.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateCheckService(HttpClient httpClient) : IUpdateCheckService
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
                return null;

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

            var isUpdateAvailable = IsRemoteVersionNewer(latestVersion, currentVersion);
            return new UpdateCheckResult(
                currentVersion,
                latestVersion,
                isUpdateAvailable,
                releaseUri,
                release.Body);
        }
        catch
        {
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
    }
}
