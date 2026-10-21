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
        _manager = new UpdateManager(new GithubSource("https://github.com/norschel/SlidevPresenter", null, includePrereleases));
        _update = await _manager.CheckForUpdatesAsync();
        return _update?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync() =>
        _manager!.DownloadUpdatesAsync(_update ?? throw new InvalidOperationException("Check for updates first."));

    public void PrepareRestart() =>
        _manager!.WaitExitThenApplyUpdates(_update!.TargetFullRelease, restart: true);
}
