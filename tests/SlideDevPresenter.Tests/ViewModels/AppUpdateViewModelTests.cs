using SlideDevPresenter.App.Services;
using SlideDevPresenter.App.ViewModels;

namespace SlideDevPresenter.Tests.ViewModels;

public sealed class AppUpdateViewModelTests
{
    [Fact]
    public void UnpackagedBuild_DisablesUpdates()
    {
        var service = new FakeUpdateService { IsInstalled = false };
        var vm = new AppUpdateViewModel(service, () => Task.CompletedTask, () => { });

        Assert.False(vm.UpdateCommand.CanExecute(null));
        Assert.Contains("Install", vm.Status);
        Assert.Equal(0, service.Checks);
    }

    [Fact]
    public async Task Update_RequiresSeparateConsentToCheckDownloadAndRestart()
    {
        var service = new FakeUpdateService();
        var events = service.Events;
        var vm = new AppUpdateViewModel(service, () =>
        {
            events.Add("cleanup");
            return Task.CompletedTask;
        }, () => events.Add("shutdown"));

        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Contains("2.0.0", vm.Status);
        Assert.Equal("Download update", vm.ButtonText);
        Assert.Empty(events);

        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Equal("Restart and install", vm.ButtonText);
        Assert.Equal(new[] { "download" }, events);

        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "download", "cleanup", "prepare", "shutdown" }, events);
    }

    [Fact]
    public async Task NoUpdate_CanCheckAgain()
    {
        var service = new FakeUpdateService { Version = null };
        var vm = new AppUpdateViewModel(service, () => Task.CompletedTask, () => { });

        await vm.UpdateCommand.ExecuteAsync(null);
        await vm.UpdateCommand.ExecuteAsync(null);

        Assert.Equal("You are up to date.", vm.Status);
        Assert.Equal(2, service.Checks);
        Assert.Empty(service.Events);
    }

    [Fact]
    public async Task FailedDownload_CanRetryWithoutRestarting()
    {
        var service = new FakeUpdateService { FailDownload = true };
        var vm = new AppUpdateViewModel(service, () => Task.CompletedTask, () => service.Events.Add("shutdown"));
        await vm.UpdateCommand.ExecuteAsync(null);

        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Contains("Update failed", vm.Status);
        Assert.Equal("Download update", vm.ButtonText);
        Assert.Empty(service.Events);

        service.FailDownload = false;
        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "download" }, service.Events);
    }

    [Fact]
    public async Task FailedCleanup_DoesNotLaunchUpdaterOrExit()
    {
        var service = new FakeUpdateService();
        var vm = new AppUpdateViewModel(service, () => throw new IOException("Cannot save"),
            () => service.Events.Add("shutdown"));
        await vm.UpdateCommand.ExecuteAsync(null);
        await vm.UpdateCommand.ExecuteAsync(null);

        await vm.UpdateCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "download" }, service.Events);
        Assert.Contains("Update failed", vm.Status);
    }

    [Fact]
    public async Task FailedCheck_CanRetry()
    {
        var service = new FakeUpdateService { FailCheck = true };
        var vm = new AppUpdateViewModel(service, () => Task.CompletedTask, () => { });

        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Contains("Update failed", vm.Status);

        service.FailCheck = false;
        await vm.UpdateCommand.ExecuteAsync(null);
        Assert.Equal("Download update", vm.ButtonText);
        Assert.Equal(2, service.Checks);
    }

    private sealed class FakeUpdateService : IAppUpdateService
    {
        public bool IsInstalled { get; set; } = true;
        public string? Version { get; set; } = "2.0.0";
        public bool FailCheck { get; set; }
        public bool FailDownload { get; set; }
        public int Checks { get; private set; }
        public List<string> Events { get; } = [];

        public Task<string?> CheckAsync()
        {
            Checks++;
            return FailCheck ? Task.FromException<string?>(new IOException("Offline")) : Task.FromResult(Version);
        }

        public Task DownloadAsync()
        {
            if (FailDownload)
                return Task.FromException(new IOException("Offline"));
            Events.Add("download");
            return Task.CompletedTask;
        }

        public void PrepareRestart() => Events.Add("prepare");
    }
}
