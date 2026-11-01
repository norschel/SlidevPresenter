using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlideDevPresenter.App.Services;

namespace SlideDevPresenter.App.ViewModels;

public sealed partial class AppUpdateViewModel : ObservableObject
{
    private readonly IAppUpdateService _service;
    private readonly Func<Task> _beforeRestart;
    private readonly Action _shutdown;
    private bool _available;
    private bool _downloaded;

    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private string _buttonText = "Check for updates";

    [ObservableProperty]
    private bool _includePrereleases;

    partial void OnIncludePrereleasesChanged(bool value)
    {
        _available = false;
        _downloaded = false;
        ButtonText = "Check for updates";
        Status = value
            ? "Unstable releases may contain bugs. Check for updates to include pre-releases."
            : "Only stable releases will be offered. Check for updates again.";
    }

    public AppUpdateViewModel(IAppUpdateService service, Func<Task> beforeRestart, Action shutdown)
    {
        _service = service;
        _beforeRestart = beforeRestart;
        _shutdown = shutdown;
        _status = service.IsInstalled
            ? "Updates are checked only when requested."
            : "Install a Windows or macOS release to enable in-app updates.";
    }

    private bool CanUpdate() => _service.IsInstalled;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateAsync()
    {
        try
        {
            if (_downloaded)
            {
                await _beforeRestart();
                _service.PrepareRestart();
                _shutdown();
            }
            else if (_available)
            {
                Status = "Downloading update…";
                await _service.DownloadAsync();
                _downloaded = true;
                ButtonText = "Restart and install";
                Status = "Update ready. Restarting will stop the current presentation.";
            }
            else
            {
                Status = "Checking GitHub Releases…";
                var version = await _service.CheckAsync(IncludePrereleases);
                _available = version is not null;
                ButtonText = _available ? "Download update" : "Check for updates";
                Status = _available ? $"Version {version} is available." : "You are up to date.";
            }
        }
        catch (Exception)
        {
            Status = "Update failed. Try again, or download the installer from GitHub Releases.";
        }
    }
}
