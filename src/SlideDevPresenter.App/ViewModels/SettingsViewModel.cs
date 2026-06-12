using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlideDevPresenter.App.Services;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    public ObservableCollection<SourceViewModel> Sources { get; } = [];
    public IReadOnlyList<string> AvailableModes { get; } = Enum.GetNames<PresentationSurfaceMode>();
    public IReadOnlyList<string> AvailableThemes { get; } = ["System", "Light", "Dark"];

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

    [ObservableProperty]
    private bool _autoDetectDisplays;

    [ObservableProperty]
    private bool _fullscreenParticipantView;

    [ObservableProperty]
    private bool _restoreDisplayTopologyOnExit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutConflictError))]
    private string _startFromBeginningShortcut = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutConflictError))]
    private string _startFromCurrentSlideShortcut = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutConflictError))]
    private string _startPresenterViewShortcut = string.Empty;

    [ObservableProperty]
    private bool _openExternalLinksInSystemBrowser;

    [ObservableProperty]
    private bool _openExternalLinksInEmbeddedBrowser;

    public string? ShortcutConflictError => ValidateShortcuts();

    public SettingsViewModel(ISettingsService settingsService)
        : this(settingsService, new ThemeService())
    {
    }

    public SettingsViewModel(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService;
        _themeService = themeService;
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

        AutoDetectDisplays = settings.DisplayManagement.AutoDetectDisplays;
        FullscreenParticipantView = settings.DisplayManagement.FullscreenParticipantView;
        RestoreDisplayTopologyOnExit = settings.DisplayManagement.RestoreDisplayTopologyOnExit;

        StartFromBeginningShortcut = settings.Shortcuts.StartFromBeginning ?? string.Empty;
        StartFromCurrentSlideShortcut = settings.Shortcuts.StartFromCurrentSlide ?? string.Empty;
        StartPresenterViewShortcut = settings.Shortcuts.StartPresenterView ?? string.Empty;

        OpenExternalLinksInSystemBrowser = settings.Navigation.OpenExternalLinksInSystemBrowser;
        OpenExternalLinksInEmbeddedBrowser = settings.Navigation.OpenExternalLinksInEmbeddedBrowser;
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
    private void RestoreDefaultShortcuts()
    {
        StartFromBeginningShortcut = string.Empty;
        StartFromCurrentSlideShortcut = string.Empty;
        StartPresenterViewShortcut = string.Empty;
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
        settings.DisplayManagement.AutoDetectDisplays = AutoDetectDisplays;
        settings.DisplayManagement.FullscreenParticipantView = FullscreenParticipantView;
        settings.DisplayManagement.RestoreDisplayTopologyOnExit = RestoreDisplayTopologyOnExit;

        settings.Shortcuts.StartFromBeginning = NullIfEmpty(StartFromBeginningShortcut);
        settings.Shortcuts.StartFromCurrentSlide = NullIfEmpty(StartFromCurrentSlideShortcut);
        settings.Shortcuts.StartPresenterView = NullIfEmpty(StartPresenterViewShortcut);

        settings.Navigation.OpenExternalLinksInSystemBrowser = OpenExternalLinksInSystemBrowser;
        settings.Navigation.OpenExternalLinksInEmbeddedBrowser = OpenExternalLinksInEmbeddedBrowser;

        await _settingsService.SaveAsync();
        _themeService.ApplyTheme(Theme);
    }

    private string? ValidateShortcuts()
    {
        var shortcuts = new[]
        {
            (Label: "Start From Beginning", Value: StartFromBeginningShortcut),
            (Label: "Start From Current Slide", Value: StartFromCurrentSlideShortcut),
            (Label: "Start Presenter View", Value: StartPresenterViewShortcut),
        };

        // Check for invalid key gestures
        foreach (var (label, value) in shortcuts)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            try { KeyGesture.Parse(value); }
            catch { return $"\"{value}\" is not a valid key gesture for \"{label}\"."; }
        }

        // Check for duplicates among non-empty values
        var nonEmpty = shortcuts
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => (s.Label, Normalized: s.Value.Trim().ToUpperInvariant()))
            .ToList();

        var duplicates = nonEmpty
            .GroupBy(s => s.Normalized)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" and ", g.Select(s => $"\"{s.Label}\"")))
            .FirstOrDefault();

        if (duplicates is not null)
            return $"Duplicate shortcut assigned to {duplicates}.";

        return null;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
