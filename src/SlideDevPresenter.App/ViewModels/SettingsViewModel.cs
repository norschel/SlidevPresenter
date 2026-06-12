using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    public ObservableCollection<SourceViewModel> Sources { get; } = [];
    public IReadOnlyList<string> AvailableModes { get; } = Enum.GetNames<PresentationSurfaceMode>();

    [ObservableProperty]
    private SourceViewModel? _selectedSource;

    [ObservableProperty]
    private int _defaultPort;

    [ObservableProperty]
    private bool _autoIncrementPorts;

    [ObservableProperty]
    private bool _openParticipantOnSecondMonitor;

    [ObservableProperty]
    private bool _fullscreenParticipant;

    [ObservableProperty]
    private bool _openPresenterOnStart;

    [ObservableProperty]
    private string _defaultMode = nameof(PresentationSurfaceMode.Presenter);

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private bool _showStatusBar;

    [ObservableProperty]
    private bool _showRibbon;

    [ObservableProperty]
    private bool _preferEmbeddedWebView;

    [ObservableProperty]
    private bool _allowExternalBrowserFallback;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromSettings(settingsService.Settings);
    }

    private void LoadFromSettings(AppSettings settings)
    {
        Sources.Clear();
        foreach (var source in settings.Sources)
            Sources.Add(new SourceViewModel(source));

        DefaultPort = settings.Defaults.DefaultPort;
        AutoIncrementPorts = settings.Defaults.AutoIncrementPorts;
        OpenParticipantOnSecondMonitor = settings.Defaults.OpenParticipantOnSecondMonitor;
        FullscreenParticipant = settings.Defaults.FullscreenParticipant;
        OpenPresenterOnStart = settings.Defaults.OpenPresenterOnStart;
        DefaultMode = settings.Defaults.DefaultMode;

        Theme = settings.Appearance.Theme;
        ShowStatusBar = settings.Appearance.ShowStatusBar;
        ShowRibbon = settings.Appearance.ShowRibbon;

        PreferEmbeddedWebView = settings.WebView.PreferEmbeddedWebView;
        AllowExternalBrowserFallback = settings.WebView.AllowExternalBrowserFallback;
    }

    [RelayCommand]
    private void AddLocalRootSource()
    {
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "New Local Root",
            Type = PresentationSourceType.LocalRoot,
            Location = "",
            IsEnabled = true
        };
        var vm = new SourceViewModel(source);
        Sources.Add(vm);
        SelectedSource = vm;
    }

    [RelayCommand]
    private void AddLocalProjectSource()
    {
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "New Local Project",
            Type = PresentationSourceType.LocalProject,
            Location = "",
            IsEnabled = true
        };
        var vm = new SourceViewModel(source);
        Sources.Add(vm);
        SelectedSource = vm;
    }

    [RelayCommand]
    private void AddHostedUrlSource()
    {
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "New Hosted Presentation",
            Type = PresentationSourceType.HostedUrl,
            Location = "https://",
            IsEnabled = true
        };
        var vm = new SourceViewModel(source);
        Sources.Add(vm);
        SelectedSource = vm;
    }

    [RelayCommand]
    private void RemoveSelectedSource()
    {
        if (SelectedSource is not null)
        {
            Sources.Remove(SelectedSource);
            SelectedSource = null;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = _settingsService.Settings;
        settings.Sources = Sources.Select(s => s.ToModel()).ToList();
        settings.Defaults.DefaultPort = DefaultPort;
        settings.Defaults.AutoIncrementPorts = AutoIncrementPorts;
        settings.Defaults.OpenParticipantOnSecondMonitor = OpenParticipantOnSecondMonitor;
        settings.Defaults.FullscreenParticipant = FullscreenParticipant;
        settings.Defaults.OpenPresenterOnStart = OpenPresenterOnStart;
        settings.Defaults.DefaultMode = DefaultMode;
        settings.Appearance.Theme = Theme;
        settings.Appearance.ShowStatusBar = ShowStatusBar;
        settings.Appearance.ShowRibbon = ShowRibbon;
        settings.WebView.PreferEmbeddedWebView = PreferEmbeddedWebView;
        settings.WebView.AllowExternalBrowserFallback = AllowExternalBrowserFallback;

        await _settingsService.SaveAsync();
    }
}
