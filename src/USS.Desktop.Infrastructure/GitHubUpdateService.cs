using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using USS.Desktop.Application;

namespace USS.Desktop.Infrastructure;

public sealed class GitHubUpdateService : IUpdateService
{
    public const string RepositoryOwner = "Driadix";
    public const string RepositoryName = "USS-Desktop";
    public const string ReleaseAssetName = "USS.Desktop-win-x64.zip";

    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public GitHubUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        EnsureGitHubHeaders(_httpClient);
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        var normalizedCurrentVersion = ApplicationVersion.Normalize(currentVersion);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UpdateCheckResult.Failed(normalizedCurrentVersion, "No published GitHub release was found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed(
                    normalizedCurrentVersion,
                    $"GitHub update check failed with HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var latestRelease = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, SerializerOptions, cancellationToken);
            if (latestRelease is null)
            {
                return UpdateCheckResult.Failed(normalizedCurrentVersion, "GitHub returned an empty release payload.");
            }

            if (!ApplicationVersion.TryParseTag(latestRelease.TagName, out var latestVersion))
            {
                return UpdateCheckResult.Failed(normalizedCurrentVersion, $"Latest release tag '{latestRelease.TagName}' is not a semantic version.");
            }

            if (ApplicationVersion.Normalize(latestVersion).CompareTo(normalizedCurrentVersion) <= 0)
            {
                return UpdateCheckResult.UpToDate(normalizedCurrentVersion, $"USS Desktop is already up to date at {ApplicationVersion.FormatForDisplay(normalizedCurrentVersion)}.");
            }

            var assets = latestRelease.Assets ?? Array.Empty<GitHubReleaseAsset>();
            var releaseAsset = assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, ReleaseAssetName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl));

            if (releaseAsset is null)
            {
                return UpdateCheckResult.Failed(normalizedCurrentVersion, $"Latest release '{latestRelease.TagName}' does not contain {ReleaseAssetName}.");
            }

            if (!Uri.TryCreate(releaseAsset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUrl)
                || !Uri.TryCreate(latestRelease.HtmlUrl, UriKind.Absolute, out var releasePageUrl))
            {
                return UpdateCheckResult.Failed(normalizedCurrentVersion, $"Latest release '{latestRelease.TagName}' contains an invalid URL.");
            }

            if (!IsSha256Digest(releaseAsset.Digest))
            {
                return UpdateCheckResult.Failed(normalizedCurrentVersion, $"Latest release '{latestRelease.TagName}' does not contain a valid SHA-256 digest for {ReleaseAssetName}.");
            }

            var release = new ApplicationRelease(
                latestRelease.TagName ?? string.Empty,
                latestVersion,
                releaseAsset.Name ?? ReleaseAssetName,
                downloadUrl,
                releasePageUrl,
                releaseAsset.Digest!.Trim());

            return UpdateCheckResult.UpdateAvailable(
                normalizedCurrentVersion,
                release,
                $"USS Desktop {ApplicationVersion.FormatForDisplay(release.Version)} is available.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            return UpdateCheckResult.Failed(normalizedCurrentVersion, $"Update check failed: {exception.Message}");
        }
    }

    private static void EnsureGitHubHeaders(HttpClient httpClient)
    {
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("USS-Desktop", "1.0"));
        }

        if (!httpClient.DefaultRequestHeaders.Accept.Any())
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        if (!httpClient.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
        {
            httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }
    }

    private static bool IsSha256Digest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmedValue = value.Trim();
        const string prefix = "sha256:";
        if (trimmedValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmedValue = trimmedValue[prefix.Length..];
        }

        return trimmedValue.Length == 64 && trimmedValue.All(Uri.IsHexDigit);
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubReleaseAsset>? Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest);
}
