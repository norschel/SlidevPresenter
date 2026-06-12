using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly Uri AboutBlankUri = new("about:blank");

    private readonly ISettingsService _settingsService;
    private readonly ISourceScanner _sourceScanner;
    private readonly ISlidevProcessHost _processHost;
    private readonly ISlideDeckMetadataReader _slideDeckMetadataReader;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly IDisplayService _displayService;
    private readonly SynchronizationContext? _syncContext;
    private CancellationTokenSource? _timerCts;
    private DateTimeOffset? _sessionStartedAt;
    private int _metadataLoadVersion;
    private bool _hasAutoOpenedForCurrentRun;
    private bool _isHostedSessionActive;
    private int? _pendingSlideNavigation;

    public ObservableCollection<PresentationProjectViewModel> Projects { get; } = [];
    public ObservableCollection<SlideDeckSlide> Slides { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private PresentationProjectViewModel? _selectedProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsStarting))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private HostState _hostState = HostState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceUrl))]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceUriOrBlank))]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceLabel))]
    [NotifyPropertyChangedFor(nameof(CanShowEmbeddedSurface))]
    [NotifyPropertyChangedFor(nameof(CanShowBrowserFallback))]
    private string? _participantUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceUrl))]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceUriOrBlank))]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceLabel))]
    [NotifyPropertyChangedFor(nameof(CanShowEmbeddedSurface))]
    [NotifyPropertyChangedFor(nameof(CanShowBrowserFallback))]
    private string? _presenterUrl;

    [ObservableProperty]
    private int? _port;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceUrl))]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceUriOrBlank))]
    [NotifyPropertyChangedFor(nameof(CurrentSurfaceLabel))]
    [NotifyPropertyChangedFor(nameof(CanShowEmbeddedSurface))]
    private PresentationSurfaceMode _selectedSurfaceMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeRibbonSelected))]
    [NotifyPropertyChangedFor(nameof(IsLibraryRibbonSelected))]
    [NotifyPropertyChangedFor(nameof(IsViewRibbonSelected))]
    [NotifyPropertyChangedFor(nameof(IsPresentationRibbonSelected))]
    private string _selectedRibbonTab = "Home";

    [ObservableProperty]
    private bool _showThumbnailsPanel = true;

    [ObservableProperty]
    private bool _showAgendaPanel = true;

    [ObservableProperty]
    private bool _showTimerPanel = true;

    [ObservableProperty]
    private string _deckTitle = "Presentation workspace";

    [ObservableProperty]
    private SlideDeckSlide? _selectedOutlineSlide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedPresentationTimeText))]
    private TimeSpan _elapsedPresentationTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetectedDisplays))]
    private int _detectedDisplayCount;

    public bool HasDetectedDisplays => DetectedDisplayCount > 0;

    public bool IsIdle => HostState == HostState.Idle;
    public bool IsStarting => HostState == HostState.Starting;
    public bool IsRunning => HostState == HostState.Running;
    public bool IsError => HostState == HostState.Error;

    public bool IsHomeRibbonSelected => SelectedRibbonTab == "Home";
    public bool IsLibraryRibbonSelected => SelectedRibbonTab == "Library";
    public bool IsViewRibbonSelected => SelectedRibbonTab == "View";
    public bool IsPresentationRibbonSelected => SelectedRibbonTab == "Presentation";

    public string CurrentSurfaceLabel => SelectedSurfaceMode == PresentationSurfaceMode.Presenter ? "Presenter View" : "Participant View";
    public string CurrentSurfaceUrl => SelectedSurfaceMode == PresentationSurfaceMode.Presenter
        ? PresenterUrl ?? "Not available"
        : ParticipantUrl ?? "Not available";
    public Uri CurrentSurfaceUriOrBlank => Uri.TryCreate(CurrentSurfaceUrl, UriKind.Absolute, out var uri) ? uri : AboutBlankUri;
    public bool CanShowEmbeddedSurface => IsRunning && _settingsService.Settings.WebView.PreferEmbeddedWebView && CurrentSurfaceUriOrBlank != AboutBlankUri;
    public bool CanShowBrowserFallback => IsRunning && _settingsService.Settings.WebView.AllowExternalBrowserFallback;
    public bool ShowRibbon => _settingsService.Settings.Appearance.ShowRibbon;
    public bool ShowStatusBar => _settingsService.Settings.Appearance.ShowStatusBar;
    public bool HasSlides => Slides.Count > 0;
    public int SlideCount => Slides.Count;
    public string ElapsedPresentationTimeText => ElapsedPresentationTime.ToString(@"hh\:mm\:ss");
    public string SelectedSlideSummary => SelectedOutlineSlide?.Summary ?? "Select a slide to inspect its summary.";
    public string EmbeddedSurfaceHint => _settingsService.Settings.WebView.PreferEmbeddedWebView
        ? "Embedded preview is waiting for a reachable presenter surface."
        : "Embedded preview is disabled in settings. Use the browser workflow instead.";

    public ISettingsService SettingsService => _settingsService;

    public MainViewModel(ISettingsService settingsService, ISourceScanner sourceScanner, ISlidevProcessHost processHost)
        : this(settingsService, sourceScanner, processHost, new NullSlideDeckMetadataReader())
    {
    }

    public MainViewModel(ISettingsService settingsService, ISourceScanner sourceScanner, ISlidevProcessHost processHost, ISlideDeckMetadataReader slideDeckMetadataReader)
        : this(settingsService, sourceScanner, processHost, slideDeckMetadataReader, new NullPresentationWindowService())
    {
    }

    public MainViewModel(ISettingsService settingsService, ISourceScanner sourceScanner, ISlidevProcessHost processHost, ISlideDeckMetadataReader slideDeckMetadataReader, IPresentationWindowService presentationWindowService)
        : this(settingsService, sourceScanner, processHost, slideDeckMetadataReader, presentationWindowService, new NullDisplayService())
    {
    }

    public MainViewModel(ISettingsService settingsService, ISourceScanner sourceScanner, ISlidevProcessHost processHost, ISlideDeckMetadataReader slideDeckMetadataReader, IPresentationWindowService presentationWindowService, IDisplayService displayService)
    {
        _settingsService = settingsService;
        _sourceScanner = sourceScanner;
        _processHost = processHost;
        _slideDeckMetadataReader = slideDeckMetadataReader;
        _presentationWindowService = presentationWindowService;
        _displayService = displayService;
        _syncContext = SynchronizationContext.Current;
        _selectedSurfaceMode = ParseSurfaceMode(settingsService.Settings.Defaults.DefaultMode);

        _processHost.StateChanged += OnHostStateChanged;
        _presentationWindowService.PresentationExited += OnPresentationExited;
    }

    public void RefreshPreferences()
    {
        SelectedSurfaceMode = ParseSurfaceMode(_settingsService.Settings.Defaults.DefaultMode);
        OnPropertyChanged(nameof(ShowRibbon));
        OnPropertyChanged(nameof(ShowStatusBar));
        OnPropertyChanged(nameof(CanShowEmbeddedSurface));
        OnPropertyChanged(nameof(CanShowBrowserFallback));
        OnPropertyChanged(nameof(EmbeddedSurfaceHint));
    }

    partial void OnSelectedProjectChanged(PresentationProjectViewModel? value)
    {
        _ = LoadSelectedProjectMetadataAsync(value);
    }

    partial void OnSelectedOutlineSlideChanged(SlideDeckSlide? value)
    {
        OnPropertyChanged(nameof(SelectedSlideSummary));
    }

    private void OnHostStateChanged(object? sender, HostStateChangedEventArgs e)
    {
        if (_isHostedSessionActive)
            return;

        void Update() => ApplyStateSnapshot(e.NewState, _processHost.ParticipantUrl, _processHost.PresenterUrl, _processHost.Port, e.ErrorMessage);

        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
            Update();
        else
            _syncContext.Post(_ => Update(), null);
    }

    [RelayCommand]
    public async Task RefreshLibraryAsync()
    {
        IsRefreshing = true;
        try
        {
            var sources = _settingsService.Settings.Sources
                .Where(s => s.IsEnabled)
                .ToList();

            var projects = new List<PresentationProjectViewModel>();
            foreach (var source in sources)
            {
                switch (source.Type)
                {
                    case PresentationSourceType.LocalRoot:
                        var scanned = await Task.Run(() => _sourceScanner.ScanRoot(source.Location));
                        projects.AddRange(scanned.Select(p => new PresentationProjectViewModel(p)));
                        break;

                    case PresentationSourceType.LocalProject:
                        if (_sourceScanner.IsSlidevProject(source.Location))
                        {
                            var expanded = SourceScanner.ExpandHomePath(source.Location);
                            var slides = Path.Combine(expanded, "slides.md");
                            projects.Add(new PresentationProjectViewModel(new PresentationProject
                            {
                                Id = source.Id,
                                Name = source.Name,
                                SourceType = PresentationSourceType.LocalProject,
                                Location = expanded,
                                SlidesFilePath = File.Exists(slides) ? slides : null
                            }));
                        }
                        break;

                    case PresentationSourceType.HostedUrl:
                        projects.Add(new PresentationProjectViewModel(new PresentationProject
                        {
                            Id = source.Id,
                            Name = source.Name,
                            SourceType = PresentationSourceType.HostedUrl,
                            Location = source.Location
                        }));
                        break;
                }
            }

            var previousSelectionId = SelectedProject?.Id;
            Projects.Clear();
            foreach (var project in projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                Projects.Add(project);

            SelectedProject = previousSelectionId is not null
                ? Projects.FirstOrDefault(project => project.Id == previousSelectionId)
                : Projects.FirstOrDefault();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    public async Task LaunchAsync()
    {
        if (SelectedProject is null)
            return;

        _hasAutoOpenedForCurrentRun = false;
        RefreshPreferences();

        if (SelectedProject.SourceType == PresentationSourceType.HostedUrl)
        {
            StartHostedSession(SelectedProject.Location);
            return;
        }

        _isHostedSessionActive = false;
        var port = _settingsService.Settings.Defaults.DefaultPort;
        await _processHost.StartAsync(SelectedProject.ToModel(), port);
    }

    private bool CanLaunch() => SelectedProject is not null && HostState == HostState.Idle;

    [RelayCommand]
    public async Task StartFromBeginningAsync()
    {
        await StartForSlideAsync(1, PresentationSurfaceMode.Presenter);
    }

    [RelayCommand]
    public async Task StartFromCurrentSlideAsync()
    {
        var currentSlide = SelectedOutlineSlide?.Number ?? 1;
        await StartForSlideAsync(currentSlide, SelectedSurfaceMode);
    }

    [RelayCommand]
    public async Task StartPresenterViewAsync()
    {
        var currentSlide = SelectedOutlineSlide?.Number ?? 1;
        await StartForSlideAsync(currentSlide, PresentationSurfaceMode.Presenter);
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    public async Task StopAsync()
    {
        if (_isHostedSessionActive)
        {
            StopHostedSession();
            return;
        }

        await _processHost.StopAsync();
    }

    private bool CanStop() => HostState is HostState.Starting or HostState.Running;

    [RelayCommand(CanExecute = nameof(CanRetry))]
    public async Task RetryAsync()
    {
        if (SelectedProject is null)
            return;

        _hasAutoOpenedForCurrentRun = false;

        if (_isHostedSessionActive || SelectedProject.SourceType == PresentationSourceType.HostedUrl)
        {
            StopHostedSession(resetElapsed: false);
            StartHostedSession(SelectedProject.Location);
            return;
        }

        await _processHost.StopAsync();
        var port = _settingsService.Settings.Defaults.DefaultPort;
        await _processHost.StartAsync(SelectedProject.ToModel(), port);
    }

    private bool CanRetry() => SelectedProject is not null && HostState == HostState.Error;

    [RelayCommand]
    public void OpenCurrentSurfaceInBrowser() => TryOpenBrowser(CurrentSurfaceUrl);

    [RelayCommand]
    public void OpenParticipantBrowser() => TryOpenBrowser(ParticipantUrl);

    [RelayCommand]
    public void OpenPresenterBrowser() => TryOpenBrowser(PresenterUrl);

    [RelayCommand]
    public void SelectHomeRibbon() => SelectedRibbonTab = "Home";

    [RelayCommand]
    public void SelectLibraryRibbon() => SelectedRibbonTab = "Library";

    [RelayCommand]
    public void SelectViewRibbon() => SelectedRibbonTab = "View";

    [RelayCommand]
    public void SelectPresentationRibbon() => SelectedRibbonTab = "Presentation";

    [RelayCommand]
    public void UsePresenterSurface() => SelectedSurfaceMode = PresentationSurfaceMode.Presenter;

    [RelayCommand]
    public void UseParticipantSurface() => SelectedSurfaceMode = PresentationSurfaceMode.Participant;

    [RelayCommand]
    public void ToggleThumbnailsPanel() => ShowThumbnailsPanel = !ShowThumbnailsPanel;

    [RelayCommand]
    public void ToggleAgendaPanel() => ShowAgendaPanel = !ShowAgendaPanel;

    [RelayCommand]
    public void ToggleTimerPanel() => ShowTimerPanel = !ShowTimerPanel;

    [RelayCommand]
    public void DetectDisplays()
    {
        DetectedDisplayCount = _displayService.GetDisplays().Count;
    }

    private void OnPresentationExited(object? sender, EventArgs e)
    {
        void Stop()
        {
            if (HostState is HostState.Running or HostState.Starting)
                _ = StopAsync();
        }

        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
            Stop();
        else
            _syncContext.Post(_ => Stop(), null);
    }

    private async Task LoadSelectedProjectMetadataAsync(PresentationProjectViewModel? projectViewModel)
    {
        var loadVersion = Interlocked.Increment(ref _metadataLoadVersion);

        if (projectViewModel is null)
        {
            ApplyDeckMetadata(loadVersion, SlideDeckMetadata.Empty("Presentation workspace"));
            return;
        }

        var metadata = await _slideDeckMetadataReader.ReadAsync(projectViewModel.ToModel());
        ApplyDeckMetadata(loadVersion, metadata);
    }

    private void ApplyDeckMetadata(int loadVersion, SlideDeckMetadata metadata)
    {
        void Update()
        {
            if (loadVersion != _metadataLoadVersion)
                return;

            DeckTitle = string.IsNullOrWhiteSpace(metadata.DeckTitle) ? "Presentation workspace" : metadata.DeckTitle;
            Slides.Clear();
            foreach (var slide in metadata.Slides)
                Slides.Add(slide);

            SelectedOutlineSlide = Slides.FirstOrDefault();
            OnPropertyChanged(nameof(HasSlides));
            OnPropertyChanged(nameof(SlideCount));
        }

        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
            Update();
        else
            _syncContext.Post(_ => Update(), null);
    }


    private void StartHostedSession(string url)
    {
        _isHostedSessionActive = true;
        ApplyStateSnapshot(
            HostState.Running,
            NormalizeHostedUrl(url),
            BuildPresenterUrl(url),
            null,
            null);
    }

    private void StopHostedSession(bool resetElapsed = true)
    {
        _isHostedSessionActive = false;
        ApplyStateSnapshot(HostState.Idle, null, null, null, null, resetElapsed);
    }

    private void ApplyStateSnapshot(HostState state, string? participantUrl, string? presenterUrl, int? port, string? errorMessage, bool resetElapsedOnIdle = true)
    {
        if (state == HostState.Running && _pendingSlideNavigation is int slideNumber)
        {
            participantUrl = BuildSlideNavigationUrl(participantUrl, slideNumber);
            presenterUrl = BuildSlideNavigationUrl(presenterUrl, slideNumber);
            _pendingSlideNavigation = null;
        }

        HostState = state;
        ParticipantUrl = participantUrl;
        PresenterUrl = presenterUrl;
        Port = port;
        ErrorMessage = errorMessage;
        OnPropertyChanged(nameof(CurrentSurfaceUrl));
        OnPropertyChanged(nameof(CurrentSurfaceUriOrBlank));
        OnPropertyChanged(nameof(CanShowEmbeddedSurface));
        OnPropertyChanged(nameof(CanShowBrowserFallback));

        if (state == HostState.Running)
        {
            StartSessionTimer();
            if (_settingsService.Settings.DisplayManagement.AutoDetectDisplays && !string.IsNullOrWhiteSpace(participantUrl))
                _ = _presentationWindowService.OpenAsync(participantUrl, presenterUrl);
            else
                AutoOpenConfiguredViews();
        }
        else if (state == HostState.Idle)
        {
            _hasAutoOpenedForCurrentRun = false;
            _ = _presentationWindowService.CloseAsync();
            StopSessionTimer(resetElapsedOnIdle);
        }
        else if (state == HostState.Error)
        {
            _ = _presentationWindowService.CloseAsync();
            StopSessionTimer(resetElapsed: false);
        }
    }

    private void TryOpenBrowser(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not open browser: {ex.Message}";
        }
    }

    private void AutoOpenConfiguredViews()
    {
        if (_hasAutoOpenedForCurrentRun || !_settingsService.Settings.WebView.AllowExternalBrowserFallback)
            return;

        _hasAutoOpenedForCurrentRun = true;

        if (_settingsService.Settings.Defaults.OpenPresenterOnStart)
            TryOpenBrowser(PresenterUrl);

        if (_settingsService.Settings.Defaults.OpenParticipantOnSecondMonitor)
            TryOpenBrowser(ParticipantUrl);
    }

    private void StartSessionTimer()
    {
        if (_sessionStartedAt is not null)
            return;

        _sessionStartedAt = DateTimeOffset.UtcNow;
        ElapsedPresentationTime = TimeSpan.Zero;
        _timerCts = new CancellationTokenSource();
        _ = RunSessionTimerAsync(_timerCts.Token);
    }

    private async Task RunSessionTimerAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
                UpdateElapsedTime();
        }
        catch (OperationCanceledException)
        {
            // Timer cancellation is expected when the session stops.
        }
    }

    private void UpdateElapsedTime()
    {
        if (_sessionStartedAt is null)
            return;

        var elapsed = DateTimeOffset.UtcNow - _sessionStartedAt.Value;
        void Update() => ElapsedPresentationTime = elapsed;

        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
            Update();
        else
            _syncContext.Post(_ => Update(), null);
    }

    private void StopSessionTimer(bool resetElapsed)
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
        _sessionStartedAt = null;
        if (resetElapsed)
            ElapsedPresentationTime = TimeSpan.Zero;
    }

    private static string NormalizeHostedUrl(string url) => url.Trim();

    private static string BuildPresenterUrl(string url)
    {
        var normalized = NormalizeHostedUrl(url);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return normalized;

        var trimmedPath = uri.AbsolutePath.TrimEnd('/');
        if (trimmedPath.EndsWith("/presenter", StringComparison.OrdinalIgnoreCase))
            return normalized;

        var builder = new UriBuilder(uri)
        {
            Path = $"{trimmedPath}/presenter/"
        };

        return builder.Uri.ToString();
    }

    private async Task StartForSlideAsync(int slideNumber, PresentationSurfaceMode surfaceMode)
    {
        var clampedSlideNumber = Math.Max(1, slideNumber);
        SelectedSurfaceMode = surfaceMode;

        if (HostState == HostState.Running)
        {
            NavigateRunningSessionToSlide(clampedSlideNumber);
            return;
        }

        _pendingSlideNavigation = clampedSlideNumber;
        await LaunchAsync();
    }

    private void NavigateRunningSessionToSlide(int slideNumber)
    {
        ParticipantUrl = BuildSlideNavigationUrl(ParticipantUrl, slideNumber);
        PresenterUrl = BuildSlideNavigationUrl(PresenterUrl, slideNumber);
        OnPropertyChanged(nameof(CurrentSurfaceUrl));
        OnPropertyChanged(nameof(CurrentSurfaceUriOrBlank));
        OnPropertyChanged(nameof(CanShowEmbeddedSurface));
        OnPropertyChanged(nameof(CanShowBrowserFallback));

        var participantUrl = ParticipantUrl;
        if (_settingsService.Settings.DisplayManagement.AutoDetectDisplays && !string.IsNullOrWhiteSpace(participantUrl))
            _ = _presentationWindowService.OpenAsync(participantUrl, PresenterUrl);
    }

    private static string? BuildSlideNavigationUrl(string? url, int slideNumber)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var builder = new UriBuilder(uri)
        {
            Fragment = $"/{Math.Max(1, slideNumber)}"
        };

        return builder.Uri.ToString();
    }

    private static PresentationSurfaceMode ParseSurfaceMode(string? mode) =>
        string.Equals(mode, nameof(PresentationSurfaceMode.Participant), StringComparison.OrdinalIgnoreCase)
            ? PresentationSurfaceMode.Participant
            : PresentationSurfaceMode.Presenter;

    private sealed class NullSlideDeckMetadataReader : ISlideDeckMetadataReader
    {
        public Task<SlideDeckMetadata> ReadAsync(PresentationProject project, CancellationToken cancellationToken = default) =>
            Task.FromResult(SlideDeckMetadata.Empty(project.Name));
    }

    private sealed class NullPresentationWindowService : IPresentationWindowService
    {
#pragma warning disable CS0067 // Event is never used — intentional null implementation
        public event EventHandler? PresentationExited;
#pragma warning restore CS0067
        public Task OpenAsync(string participantUrl, string? presenterUrl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullDisplayService : IDisplayService
    {
        public IReadOnlyList<DisplayInfo> GetDisplays() => [];
    }
}
