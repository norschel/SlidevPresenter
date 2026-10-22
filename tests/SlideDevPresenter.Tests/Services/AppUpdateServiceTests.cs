using System.Text.Json;
using SlideDevPresenter.App.Services;
using Velopack.Sources;

namespace SlideDevPresenter.Tests.Services;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public async Task StableReleaseDiscovery_FindsStableReleaseAfterTenNewerPrereleases()
    {
        var releases = Enumerable.Range(1, 10)
            .Select(index => new
            {
                name = $"2.1.0-beta.{index}",
                prerelease = true,
                published_at = DateTimeOffset.UtcNow.AddMinutes(-index),
                assets = Array.Empty<object>()
            })
            .Append(new
            {
                name = "2.0.0",
                prerelease = false,
                published_at = DateTimeOffset.UtcNow.AddHours(-1),
                assets = Array.Empty<object>()
            });
        var downloader = new FakeFileDownloader(JsonSerializer.Serialize(releases));
        var source = new TestStableGithubSource(downloader);

        var found = await source.ReadReleasesAsync();

        Assert.Single(found);
        Assert.False(found[0].Prerelease);
        Assert.Equal("2.0.0", found[0].Name);
        Assert.Equal(
            "https://api.github.com/repos/norschel/SlidevPresenter/releases?per_page=100&page=1",
            Assert.Single(downloader.RequestedUrls));
    }

    private sealed class TestStableGithubSource(IFileDownloader downloader)
        : StableGithubSource("https://github.com/norschel/SlidevPresenter", null, false, downloader)
    {
        public Task<GithubRelease[]> ReadReleasesAsync() => base.GetReleases(false);
    }

    private sealed class FakeFileDownloader(string response) : IFileDownloader
    {
        public List<string> RequestedUrls { get; } = [];

        public Task DownloadFile(
            string url,
            string targetFile,
            Action<int> progress,
            IDictionary<string, string>? headers,
            double timeout,
            CancellationToken cancelToken) =>
            throw new NotSupportedException();

        public Task<byte[]> DownloadBytes(
            string url,
            IDictionary<string, string>? headers,
            double timeout) =>
            throw new NotSupportedException();

        public Task<string> DownloadString(
            string url,
            IDictionary<string, string>? headers,
            double timeout)
        {
            RequestedUrls.Add(url);
            return Task.FromResult(response);
        }
    }
}
