using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace SlideDevPresenter.App.Services;

public interface IAppUpdateService
{
    bool IsInstalled { get; }
    Task<string?> CheckAsync(bool includePrereleases);
    Task DownloadAsync();
    void PrepareRestart();
}

public sealed class AppUpdateService : IAppUpdateService
{
    private UpdateManager? _manager = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? new UpdateManager(new GithubSource("https://github.com/norschel/SlidevPresenter", null, false))
        : null;
    private UpdateInfo? _update;

    public bool IsInstalled => _manager?.IsInstalled == true;

    public async Task<string?> CheckAsync(bool includePrereleases)
    {
        if (!IsInstalled)
            return null;

        _update = null;
        _manager = new UpdateManager(new StableGithubSource("https://github.com/norschel/SlidevPresenter", null, includePrereleases));
        _update = await _manager.CheckForUpdatesAsync();
        return _update?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync() =>
        _manager!.DownloadUpdatesAsync(_update ?? throw new InvalidOperationException("Check for updates first."));

    public void PrepareRestart() =>
        _manager!.WaitExitThenApplyUpdates(_update!.TargetFullRelease, restart: true);
}

internal class StableGithubSource : GithubSource
{
    public StableGithubSource(string repoUrl, string? accessToken, bool prerelease, IFileDownloader? downloader = null)
        : base(repoUrl, accessToken, prerelease, downloader)
    {
    }

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        if (includePrereleases)
            return await base.GetReleases(true).ConfigureAwait(false);

        const int pageSize = 100;
        for (var page = 1; ; page++)
        {
            var releasesUri = new Uri(GetApiBaseUrl(RepoUri),
                $"repos{RepoUri.AbsolutePath}/releases?per_page={pageSize}&page={page}");
            var response = await Downloader.DownloadString(
                releasesUri.ToString(),
                GetRequestHeaders("application/vnd.github.v3+json")
            ).ConfigureAwait(false);
            var releases = JsonSerializer.Deserialize<GithubRelease[]>(response) ?? [];
            var stableRelease = releases
                .Where(release => !release.Prerelease)
                .OrderByDescending(release => release.PublishedAt)
                .FirstOrDefault();
            if (stableRelease is not null)
                return [stableRelease];
            if (releases.Length < pageSize)
                return [];
        }
    }
}
