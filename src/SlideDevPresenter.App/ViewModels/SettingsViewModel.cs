using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlideDevPresenter.App.Services;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IGitRepositoryService _gitRepositoryService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGitSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddGitHubSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadBranchesCommand))]
    private bool _isGitAvailable;

    [ObservableProperty]
    private string? _sourceMessage;

    public ObservableCollection<SourceViewModel> Sources { get; } = [];
    public ObservableCollection<string> Branches { get; } = [];
    public IReadOnlyList<string> AvailableSourceTypes { get; } = ["Local", nameof(PresentationSourceType.HostedUrl), nameof(PresentationSourceType.Git), nameof(PresentationSourceType.GitHub)];
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

    [ObservableProperty]
    private string? _configurationMessage;

    public string? ShortcutConflictError => ValidateShortcuts();

    public SettingsViewModel(ISettingsService settingsService)
        : this(settingsService, new ThemeService())
    {
    }

    public SettingsViewModel(ISettingsService settingsService, IThemeService themeService, IGitRepositoryService? gitRepositoryService = null)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _gitRepositoryService = gitRepositoryService ?? new GitRepositoryService();
        LoadFromSettings(settingsService.Settings);
    }

    public async Task CheckGitAvailabilityAsync()
    {
        IsGitAvailable = await _gitRepositoryService.IsGitAvailableAsync();
        SourceMessage = IsGitAvailable ? null : GitRepositoryService.InstallHint;
        foreach (var source in Sources)
            source.IsGitAvailable = IsGitAvailable;
    }

    partial void OnSelectedSourceChanged(SourceViewModel? value) => Branches.Clear();

    [RelayCommand(CanExecute = nameof(IsGitAvailable))]
    private async Task LoadBranchesAsync()
    {
        Branches.Clear();
        if (SelectedSource?.IsRepository != true)
            return;
        try
        {
            foreach (var branch in await _gitRepositoryService.GetBranchesAsync(SelectedSource.ToModel()))
                Branches.Add(branch);
            SourceMessage = Branches.Count == 0 ? "No cached branches found. Enter a branch name, or clone and refresh the repository first." : null;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            SourceMessage = exception.Message;
        }
    }

    private void LoadFromSettings(AppSettings settings)
    {
        Sources.Clear();
        foreach (var source in settings.Sources)
            Sources.Add(new SourceViewModel(source) { IsGitAvailable = IsGitAvailable });

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

    public void AddLocalSource(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath)),
            Type = PresentationSourceType.LocalRoot,
            Location = folderPath,
            IsEnabled = true
        };
        if (string.IsNullOrEmpty(source.Name))
            source.Name = folderPath;
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

    [RelayCommand(CanExecute = nameof(IsGitAvailable))]
    private void AddGitSource() => AddRepositorySource(PresentationSourceType.Git);

    [RelayCommand(CanExecute = nameof(IsGitAvailable))]
    private void AddGitHubSource() => AddRepositorySource(PresentationSourceType.GitHub);

    private void AddRepositorySource(PresentationSourceType type)
    {
        if (!IsGitAvailable)
            return;
        var source = new SourceViewModel(new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = type == PresentationSourceType.GitHub ? "New GitHub Presentation" : "New Git Presentation",
            Type = type,
            Location = type == PresentationSourceType.GitHub ? "owner/repository" : "https://"
        }) { IsGitAvailable = IsGitAvailable };
        Sources.Add(source);
        SelectedSource = source;
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
        foreach (var source in Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Name) || string.IsNullOrWhiteSpace(source.Location))
            {
                SourceMessage = "Each source needs a name and a location.";
                return;
            }
            if (source.IsLocal && File.Exists(SourceScanner.ExpandHomePath(source.Location.Trim())))
            {
                SourceMessage = $"{source.Name}: Select a local folder, not a file.";
                return;
            }
            if (source.IsRepository)
            {
                try { GitRepositoryService.NormalizeRemote(source.Location, source.Type); }
                catch (ArgumentException exception)
                {
                    SourceMessage = $"{source.Name}: {exception.Message}";
                    return;
                }
            }
            else if (source.Type == PresentationSourceType.HostedUrl
                && (!Uri.TryCreate(source.Location, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            {
                SourceMessage = $"{source.Name}: Use a valid HTTP or HTTPS presentation URL.";
                return;
            }
        }
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
        ConfigurationMessage = "Settings saved.";
    }

    public async Task ExportConfigurationAsync(Stream destination)
    {
        try
        {
            await _settingsService.ExportAsync(destination);
            ConfigurationMessage = "Configuration exported.";
        }
        catch (IOException ex)
        {
            ConfigurationMessage = $"Export failed: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            ConfigurationMessage = $"Export failed: {ex.Message}";
        }
    }

    public async Task ImportConfigurationAsync(Stream source)
    {
        try
        {
            await _settingsService.ImportAsync(source);
            LoadFromSettings(_settingsService.Settings);
            _themeService.ApplyTheme(Theme);
            ConfigurationMessage = "Configuration imported.";
        }
        catch (IOException ex)
        {
            ConfigurationMessage = $"Import failed: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            ConfigurationMessage = $"Import failed: {ex.Message}";
        }
        catch (InvalidDataException ex)
        {
            ConfigurationMessage = $"Import failed: {ex.Message}";
        }
    }

    public async Task ExportSourceListAsync(Stream destination)
    {
        try
        {
            await PresentationSourceListSerializer.ExportAsync(
                Sources.Select(source => source.ToModel()),
                destination);
            ConfigurationMessage = "Source list exported.";
        }
        catch (IOException ex)
        {
            ConfigurationMessage = $"Export failed: {ex.Message}";
        }
    }

    public async Task ImportSourceListAsync(Stream source)
    {
        try
        {
            var importedSources = await PresentationSourceListSerializer.ImportAsync(source);
            foreach (var importedSource in importedSources)
                Sources.Add(new SourceViewModel(importedSource) { IsGitAvailable = IsGitAvailable });
            ConfigurationMessage = "Source list imported.";
        }
        catch (IOException ex)
        {
            ConfigurationMessage = $"Import failed: {ex.Message}";
        }
        catch (InvalidDataException ex)
        {
            ConfigurationMessage = $"Import failed: {ex.Message}";
        }
        catch (System.Text.Json.JsonException ex)
        {
            ConfigurationMessage = $"Import failed: {ex.Message}";
        }
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
