using System.Net;
using System.Text;
using USS.Desktop.Application;
using USS.Desktop.Infrastructure;

namespace USS.Desktop.Tests;

public sealed class GitHubUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_WhenLatestReleaseIsNewer_ReturnsUpdateAvailable()
    {
        var service = new GitHubUpdateService(CreateHttpClient("""
            {
              "tag_name": "v0.9.3",
              "html_url": "https://github.com/Driadix/USS-Desktop/releases/tag/v0.9.3",
              "assets": [
                {
                  "name": "USS.Desktop-win-x64.zip",
                  "browser_download_url": "https://github.com/Driadix/USS-Desktop/releases/download/v0.9.3/USS.Desktop-win-x64.zip",
                  "digest": "sha256:684c8361e2acd04d47be884c4ec0169aa488859e18f00e3689c4413bd85142bb"
                }
              ]
            }
            """));

        var result = await service.CheckForUpdateAsync(new Version(0, 9, 2));

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(0, 9, 3, 0), result.Release?.Version);
        Assert.Equal("USS.Desktop-win-x64.zip", result.Release?.AssetName);
        Assert.Equal("sha256:684c8361e2acd04d47be884c4ec0169aa488859e18f00e3689c4413bd85142bb", result.Release?.Sha256Digest);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenLatestReleaseMatchesCurrentVersion_ReturnsUpToDate()
    {
        var service = new GitHubUpdateService(CreateHttpClient("""
            {
              "tag_name": "v0.9.3",
              "html_url": "https://github.com/Driadix/USS-Desktop/releases/tag/v0.9.3",
              "assets": []
            }
            """));

        var result = await service.CheckForUpdateAsync(new Version(0, 9, 3));

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.Release);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenExpectedAssetIsMissing_ReturnsFailed()
    {
        var service = new GitHubUpdateService(CreateHttpClient("""
            {
              "tag_name": "v0.9.3",
              "html_url": "https://github.com/Driadix/USS-Desktop/releases/tag/v0.9.3",
              "assets": [
                {
                  "name": "source.zip",
                  "browser_download_url": "https://github.com/Driadix/USS-Desktop/archive/refs/tags/v0.9.3.zip"
                }
              ]
            }
            """));

        var result = await service.CheckForUpdateAsync(new Version(0, 9, 2));

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Contains("does not contain", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenExpectedAssetDigestIsMissing_ReturnsFailed()
    {
        var service = new GitHubUpdateService(CreateHttpClient("""
            {
              "tag_name": "v0.9.3",
              "html_url": "https://github.com/Driadix/USS-Desktop/releases/tag/v0.9.3",
              "assets": [
                {
                  "name": "USS.Desktop-win-x64.zip",
                  "browser_download_url": "https://github.com/Driadix/USS-Desktop/releases/download/v0.9.3/USS.Desktop-win-x64.zip"
                }
              ]
            }
            """));

        var result = await service.CheckForUpdateAsync(new Version(0, 9, 2));

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.Contains("SHA-256 digest", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateHttpClient(string responseJson)
    {
        var handler = new StubHandler(responseJson);
        return new HttpClient(handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public StubHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("/repos/Driadix/USS-Desktop/releases/latest", request.RequestUri?.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
