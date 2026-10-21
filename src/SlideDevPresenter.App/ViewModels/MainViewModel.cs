using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlideDevPresenter.App.Services;
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
    private readonly IDisplaySelector _displaySelector;
    private readonly ISlideDeckPdfExporter _slideDeckPdfExporter;
    private readonly IGitRepositoryService _gitRepositoryService;
    private int _branchLoadVersion;
    private readonly ICodeEditorService _codeEditorService;
    private readonly IExternalBrowserLauncher _browserLauncher;
    private IReadOnlyList<CodeEditorEdition> _availableCodeEditors = [];
    public Func<string, Task<bool>> ConfirmRepositoryUpdateAsync { get; set; } = _ => Task.FromResult(false);

    [ObservableProperty]
    private string? _sourceMessage;
    private readonly SynchronizationContext? _syncContext;
    private CancellationTokenSource? _timerCts;
    private DateTimeOffset? _sessionStartedAt;
    private int _metadataLoadVersion;
    private bool _hasAutoOpenedForCurrentRun;
    private bool _isHostedSessionActive;
    private int? _pendingSlideNavigation;
    private string? _lastKnownParticipantUrl;
    private string? _lastKnownPresenterUrl;
    // Resolved once per running session so the display question is asked at most once per start.
    private Task<ParticipantPlacement>? _sessionPlacement;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    private bool _hasLastKnownPosition;

    public ObservableCollection<PresentationProjectViewModel> Projects { get; } = [];
    public ObservableCollection<SlideDeckSlide> Slides { get; } = [];
    public ObservableCollection<string> AvailableBranches { get; } = [];

    [ObservableProperty]
    private string? _selectedBranch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRepositoryProjectSelected))]
    [NotifyPropertyChangedFor(nameof(CanChangeSelectedBranch))]
    [NotifyPropertyChangedFor(nameof(CanExportPdf))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelectedProject))]
    [NotifyPropertyChangedFor(nameof(ShowEditTab))]
    [NotifyCanExecuteChangedFor(nameof(EditSelectedProjectCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedProjectFromLibraryCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedProjectLocationCommand))]
    private PresentationProjectViewModel? _selectedProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExportPdf))]
    private bool _isExportingPdf;

    [ObservableProperty]
    private string? _pdfExportMessage;

    [ObservableProperty]
    private string? _libraryMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeSelectedBranch))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsStarting))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
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
    [NotifyPropertyChangedFor(nameof(CanChangeSelectedBranch))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
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
    [NotifyPropertyChangedFor(nameof(IsBrowserRibbonSelected))]
    [NotifyPropertyChangedFor(nameof(IsEditRibbonSelected))]
    [NotifyPropertyChangedFor(nameof(IsNormalWorkspaceVisible))]
    [NotifyPropertyChangedFor(nameof(IsBrowserSurfaceVisible))]
    [NotifyPropertyChangedFor(nameof(CanShowEmbeddedSurface))]
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrowserWorkspaceUri))]
    [NotifyPropertyChangedFor(nameof(HasBrowserTabs))]
    [NotifyPropertyChangedFor(nameof(IsBrowserSurfaceVisible))]
    private BrowserTabViewModel? _selectedBrowserTab;

    public ObservableCollection<BrowserTabViewModel> BrowserTabs { get; } = [];

    public bool HasDetectedDisplays => DetectedDisplayCount > 0;

    public bool IsIdle => HostState == HostState.Idle;
    public bool IsStarting => HostState == HostState.Starting;
    public bool IsRunning => HostState == HostState.Running;
    public bool IsError => HostState == HostState.Error;

    public bool IsHomeRibbonSelected => SelectedRibbonTab == "Home";
    public bool IsLibraryRibbonSelected => SelectedRibbonTab == "Library";
    public bool IsViewRibbonSelected => SelectedRibbonTab == "View";
    public bool IsPresentationRibbonSelected => SelectedRibbonTab == "Presentation";
    public bool IsBrowserRibbonSelected => SelectedRibbonTab == "Browser";
    public bool IsEditRibbonSelected => SelectedRibbonTab == "Edit";
    public bool ShowEditTab => _settingsService.Settings.Editing.Enabled
        && _availableCodeEditors.Contains(_settingsService.Settings.Editing.Edition)
        && SelectedProject is { SourceType: not PresentationSourceType.HostedUrl };
    public bool IsNormalWorkspaceVisible => SelectedRibbonTab != "Browser";
    public bool HasBrowserTabs => BrowserTabs.Count > 0;
    /// <summary>
    /// True only when the Browser ribbon tab is selected AND at least one browser tab is open.
    /// Native WebView controls render in their own airspace and ignore a collapsed parent on some
    /// platforms (e.g. macOS), so the embedded BrowserWebView must be hidden explicitly whenever the
    /// Browser ribbon is not the active tab; otherwise it keeps painting over the other workspaces.
    /// </summary>
    public bool IsBrowserSurfaceVisible => IsBrowserRibbonSelected && HasBrowserTabs;
    public bool IsRepositoryProjectSelected => GetRepositorySource(SelectedProject) is not null;
    public bool CanChangeSelectedBranch => IsRepositoryProjectSelected && AvailableBranches.Count > 0
        && !IsRefreshing && HostState == HostState.Idle;
    public Uri BrowserWorkspaceUri => SelectedBrowserTab?.Url ?? AboutBlankUri;

    public string CurrentSurfaceLabel => SelectedSurfaceMode == PresentationSurfaceMode.Presenter ? "Presenter View" : "Participant View";
    public string CurrentSurfaceUrl => SelectedSurfaceMode == PresentationSurfaceMode.Presenter
        ? PresenterUrl ?? "Not available"
        : ParticipantUrl ?? "Not available";
    public Uri CurrentSurfaceUriOrBlank => Uri.TryCreate(CurrentSurfaceUrl, UriKind.Absolute, out var uri) ? uri : AboutBlankUri;
    public bool CanShowEmbeddedSurface => IsRunning && IsNormalWorkspaceVisible && _settingsService.Settings.WebView.PreferEmbeddedWebView && CurrentSurfaceUriOrBlank != AboutBlankUri;
    public bool CanShowBrowserFallback => IsRunning && _settingsService.Settings.WebView.AllowExternalBrowserFallback;
    public bool ShowRibbon => _settingsService.Settings.Appearance.ShowRibbon;
    public bool ShowStatusBar => _settingsService.Settings.Appearance.ShowStatusBar;
    public bool HasSlides => Slides.Count > 0;
    public bool CanExportPdf => SelectedProject?.SourceType == PresentationSourceType.LocalProject && !IsExportingPdf;
    /// <summary>Only projects that map 1:1 to a configured source can be removed; root-scanned projects are managed via their root source.</summary>
    public bool CanRemoveSelectedProject => SelectedProject is not null && _settingsService.Settings.Sources.Any(s => s.Id == SelectedProject.Id);
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

    public MainViewModel(ISettingsService settingsService, ISourceScanner sourceScanner, ISlidevProcessHost processHost, ISlideDeckMetadataReader slideDeckMetadataReader, IPresentationWindowService presentationWindowService, IDisplayService displayService, ISlideDeckPdfExporter? slideDeckPdfExporter = null, IDisplaySelector? displaySelector = null, IGitRepositoryService? gitRepositoryService = null, ICodeEditorService? codeEditorService = null, IExternalBrowserLauncher? browserLauncher = null)
    {
        _settingsService = settingsService;
        _sourceScanner = sourceScanner;
        _processHost = processHost;
        _slideDeckMetadataReader = slideDeckMetadataReader;
        _presentationWindowService = presentationWindowService;
        _displayService = displayService;
        _displaySelector = displaySelector ?? new DefaultDisplaySelector();
        _slideDeckPdfExporter = slideDeckPdfExporter ?? new SlidevPdfExporter();
        _gitRepositoryService = gitRepositoryService ?? new GitRepositoryService();
        _codeEditorService = codeEditorService ?? new CodeEditorService();
        // No-op by default so headless/test usage never launches the system browser.
        _browserLauncher = browserLauncher ?? new NullExternalBrowserLauncher();
        _availableCodeEditors = _codeEditorService.GetAvailableEditions();
        _syncContext = SynchronizationContext.Current;
        _selectedSurfaceMode = ParseSurfaceMode(settingsService.Settings.Defaults.DefaultMode);

        _processHost.StateChanged += OnHostStateChanged;
        _presentationWindowService.PresentationExited += OnPresentationExited;
        _presentationWindowService.ExternalLinkNavigated += OnExternalLinkNavigated;
    }

    public void RefreshPreferences()
    {
        SelectedSurfaceMode = ParseSurfaceMode(_settingsService.Settings.Defaults.DefaultMode);
        OnPropertyChanged(nameof(ShowRibbon));
        OnPropertyChanged(nameof(ShowStatusBar));
        OnPropertyChanged(nameof(CanShowEmbeddedSurface));
        OnPropertyChanged(nameof(CanShowBrowserFallback));
        OnPropertyChanged(nameof(EmbeddedSurfaceHint));
        _availableCodeEditors = _codeEditorService.GetAvailableEditions();
        OnPropertyChanged(nameof(ShowEditTab));
        EditSelectedProjectCommand.NotifyCanExecuteChanged();
        ResetEditTabIfHidden();
    }

    partial void OnSelectedProjectChanged(PresentationProjectViewModel? value)
    {
        ResetEditTabIfHidden();
        _ = LoadSelectedProjectMetadataAsync(value);
        _ = LoadSelectedRepositoryBranchesAsync(value);
    }

    private void ResetEditTabIfHidden()
    {
        if (!ShowEditTab && IsEditRibbonSelected)
            SelectedRibbonTab = "Home";
    }

    [RelayCommand(CanExecute = nameof(ShowEditTab))]
    private void EditSelectedProject()
    {
        if (!ShowEditTab || SelectedProject is not { } project)
            return;
        try
        {
            _codeEditorService.OpenProject(Path.GetFullPath(SourceScanner.ExpandHomePath(project.Location)), _settingsService.Settings.Editing.Edition);
            LibraryMessage = null;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            LibraryMessage = $"Could not open VS Code: {exception.Message}";
        }
    }

    [RelayCommand]
    private void SelectEditRibbon()
    {
        if (ShowEditTab)
            SelectedRibbonTab = "Edit";
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
    public async Task RefreshLibraryAsync() => await RefreshLibraryCoreAsync();

    private async Task RefreshLibraryCoreAsync(PresentationSource? changingSource = null, string? previousBranch = null)
    {
        if (IsRefreshing)
            return;
        if (HostState is HostState.Starting or HostState.Running
            && _settingsService.Settings.Sources.Any(source => source.IsEnabled && source.Type is PresentationSourceType.Git or PresentationSourceType.GitHub))
        {
            SourceMessage = "Stop the presentation before refreshing repository sources or switching branches.";
            return;
        }
        IsRefreshing = true;
        try
        {
            var messages = new List<string>();
            SourceMessage = "Refreshing sources...";
            var sources = _settingsService.Settings.Sources
                .Where(s => s.IsEnabled)
                .ToList();

            var projects = new List<PresentationProjectViewModel>();
            bool? gitAvailable = null;
            foreach (var source in sources)
            {
                if (source.Type is PresentationSourceType.Git or PresentationSourceType.GitHub)
                {
                    gitAvailable ??= await _gitRepositoryService.IsGitAvailableAsync();
                    if (!gitAvailable.Value)
                    {
                        if (!messages.Contains(GitRepositoryService.InstallHint))
                            messages.Add(GitRepositoryService.InstallHint);
                        continue;
                    }
                    var prepared = await _gitRepositoryService.PrepareAsync(source, ConfirmRepositoryUpdateAsync);
                    if (prepared.BranchSwitchDeclined && changingSource?.Id == source.Id)
                    {
                        source.Branch = previousBranch;
                        SelectedBranch = previousBranch;
                        try
                        {
                            await _settingsService.SaveAsync();
                        }
                        catch (Exception exception)
                        {
                            messages.Add($"Could not restore the selected branch in settings. {exception.Message}");
                        }
                    }
                    if (prepared.Message is not null)
                        messages.Add(prepared.Message);
                    if (prepared.Directory is not null)
                    {
                        var scanned = await Task.Run(() => _sourceScanner.ScanRoot(prepared.Directory));
                        foreach (var project in scanned)
                        {
                            projects.Add(new PresentationProjectViewModel(new PresentationProject
                            {
                                Id = CreateRepositoryProjectId(source.Id, Path.GetRelativePath(prepared.Directory, project.Location)),
                                Name = Path.GetFullPath(project.Location) == Path.GetFullPath(prepared.Directory) ? source.Name : $"{source.Name} / {project.Name}",
                                SourceType = PresentationSourceType.LocalProject,
                                Location = project.Location,
                                SlidesFilePath = project.SlidesFilePath
                            }, source.Id));
                        }
                        if (scanned.Count == 0)
                            messages.Add($"No Slidev presentations found in {prepared.Directory}. Check the checkout and slides.md, then refresh.");
                    }
                    continue;
                }
                if (LooksLikeHostedUrl(source.Location))
                {
                    projects.Add(new PresentationProjectViewModel(new PresentationProject
                    {
                        Id = source.Id,
                        Name = source.Name,
                        SourceType = PresentationSourceType.HostedUrl,
                        Location = NormalizeHostedUrl(source.Location)
                    }));
                    continue;
                }

                switch (source.Type)
                {
                    case PresentationSourceType.LocalRoot:
                        var scanned = await Task.Run(() => _sourceScanner.ScanRoot(source.Location));
                        projects.AddRange(scanned.Select(p => new PresentationProjectViewModel(p)));
                        if (scanned.Count == 0)
                            messages.Add($"{source.Name}: No Slidev presentations found in {source.Location}. A presentation folder needs slides.md, a .slidev folder, or a package.json referencing Slidev.");
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
                        else
                        {
                            messages.Add($"{source.Name}: {source.Location} is not a Slidev presentation. It needs slides.md, a .slidev folder, or a package.json referencing Slidev.");
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
                ? Projects.FirstOrDefault(project => project.Id == previousSelectionId) ?? Projects.FirstOrDefault()
                : Projects.FirstOrDefault();
            SourceMessage = messages.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, messages);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private static Guid CreateRepositoryProjectId(Guid sourceId, string relativePath)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(sourceId + ":" + relativePath);
        return new Guid(System.Security.Cryptography.SHA256.HashData(bytes).AsSpan(0, 16));
    }

    private PresentationSource? GetRepositorySource(PresentationProjectViewModel? project) =>
        project?.RepositorySourceId is Guid sourceId
            ? _settingsService.Settings.Sources.FirstOrDefault(source => source.Id == sourceId
                && source.Type is PresentationSourceType.Git or PresentationSourceType.GitHub)
            : null;

    private async Task LoadSelectedRepositoryBranchesAsync(PresentationProjectViewModel? project)
    {
        var version = ++_branchLoadVersion;
        AvailableBranches.Clear();
        SelectedBranch = null;
        OnPropertyChanged(nameof(IsRepositoryProjectSelected));
        OnPropertyChanged(nameof(CanChangeSelectedBranch));

        var source = GetRepositorySource(project);
        if (source is null)
            return;

        SelectedBranch = source.Branch;
        try
        {
            var branches = await _gitRepositoryService.GetBranchesAsync(source);
            if (version != _branchLoadVersion)
                return;

            foreach (var branch in branches)
                AvailableBranches.Add(branch);
            if (!string.IsNullOrWhiteSpace(source.Branch) && !AvailableBranches.Contains(source.Branch))
                AvailableBranches.Insert(0, source.Branch);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            if (version == _branchLoadVersion)
                SourceMessage = exception.Message;
        }

        OnPropertyChanged(nameof(CanChangeSelectedBranch));
    }

    public async Task ChangeSelectedBranchAsync(string branch)
    {
        var source = GetRepositorySource(SelectedProject);
        if (source is null || !CanChangeSelectedBranch || !AvailableBranches.Contains(branch))
            return;

        if (string.Equals(source.Branch, branch, StringComparison.Ordinal))
            return;

        var previousBranch = source.Branch;
        source.Branch = branch;
        SelectedBranch = branch;
        IsRefreshing = true;
        try
        {
            await _settingsService.SaveAsync();
        }
        catch (Exception exception)
        {
            source.Branch = previousBranch;
            SelectedBranch = previousBranch;
            SourceMessage = $"Could not save the selected branch. {exception.Message}";
            return;
        }
        finally
        {
            IsRefreshing = false;
        }

        await RefreshLibraryCoreAsync(source, previousBranch);
    }

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    public async Task LaunchAsync()
    {
        if (IsRefreshing)
        {
            SourceMessage = "Wait for the source refresh to finish before starting a presentation.";
            return;
        }
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

    private bool CanLaunch() => !IsRefreshing && SelectedProject is not null && HostState == HostState.Idle;

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedProject))]
    public async Task RemoveSelectedProjectFromLibraryAsync()
    {
        if (SelectedProject is null)
            return;

        var name = SelectedProject.Name;
        if (_settingsService.Settings.Sources.RemoveAll(s => s.Id == SelectedProject.Id) == 0)
            return;

        await _settingsService.SaveAsync();
        LibraryMessage = $"Removed \"{name}\" from the library.";
        await RefreshLibraryAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProject))]
    public void OpenSelectedProjectLocation()
    {
        if (SelectedProject is not null)
            TryOpenBrowser(SelectedProject.Location);
    }

    private bool HasSelectedProject() => SelectedProject is not null;

    public async Task ExportLibraryAsync(Stream destination)
    {
        await PresentationSourceListSerializer.ExportAsync(_settingsService.Settings.Sources, destination);
        LibraryMessage = "Library exported.";
    }

    public async Task ImportLibraryAsync(Stream source)
    {
        var imported = await PresentationSourceListSerializer.ImportAsync(source);
        var sources = _settingsService.Settings.Sources;
        var added = 0;
        foreach (var candidate in imported)
        {
            if (sources.Any(existing => string.Equals(existing.Location, candidate.Location, StringComparison.OrdinalIgnoreCase)))
                continue;

            sources.Add(candidate);
            added++;
        }

        if (added > 0)
            await _settingsService.SaveAsync();

        var skipped = imported.Count - added;
        LibraryMessage = skipped > 0
            ? $"Imported {added} source(s), skipped {skipped} already in the library."
            : $"Imported {added} source(s).";
        await RefreshLibraryAsync();
    }

    public async Task ExportPdfAsync(string outputPath)
    {
        if (!CanExportPdf || SelectedProject is null)
            return;

        IsExportingPdf = true;
        PdfExportMessage = "Exporting PDF...";
        try
        {
            await _slideDeckPdfExporter.ExportPdfAsync(SelectedProject.ToModel(), outputPath);
            PdfExportMessage = $"PDF exported to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception exception)
        {
            PdfExportMessage = $"PDF export failed: {exception.Message}";
        }
        finally
        {
            IsExportingPdf = false;
        }
    }

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

    [RelayCommand(CanExecute = nameof(CanResume))]
    public async Task ResumeAsync()
    {
        var slideNumber = ExtractSlideNumber(_lastKnownParticipantUrl) ?? 1;
        await StartForSlideAsync(slideNumber, SelectedSurfaceMode);
    }

    private bool CanResume() => !IsRefreshing && IsIdle && SelectedProject is not null && HasLastKnownPosition;

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

    private bool CanRetry() => !IsRefreshing && SelectedProject is not null && HostState == HostState.Error;

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
    public void SelectBrowserRibbon() => SelectedRibbonTab = "Browser";

    [RelayCommand]
    public void CloseBrowserTab(BrowserTabViewModel tab)
    {
        BrowserTabs.Remove(tab);
        if (SelectedBrowserTab == tab)
            SelectedBrowserTab = BrowserTabs.LastOrDefault();
        OnPropertyChanged(nameof(HasBrowserTabs));
        OnPropertyChanged(nameof(IsBrowserSurfaceVisible));
        OnPropertyChanged(nameof(BrowserWorkspaceUri));
    }

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

    private void OnExternalLinkNavigated(object? sender, Uri uri)
    {
        if (!_settingsService.Settings.Navigation.OpenExternalLinksInEmbeddedBrowser)
            return;

        void Open() => OpenInEmbeddedBrowser(uri, switchToBrowserRibbon: false);

        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
            Open();
        else
            _syncContext.Post(_ => Open(), null);
    }

    /// <summary>Opens the given URL in the embedded browser workspace.</summary>
    public void OpenInEmbeddedBrowser(Uri uri, bool switchToBrowserRibbon = true)
    {
        var existing = BrowserTabs.FirstOrDefault(t => t.Url == uri);
        if (existing is not null)
        {
            SelectedBrowserTab = existing;
        }
        else
        {
            var tab = new BrowserTabViewModel(uri);
            BrowserTabs.Add(tab);
            SelectedBrowserTab = tab;
            OnPropertyChanged(nameof(HasBrowserTabs));
            OnPropertyChanged(nameof(IsBrowserSurfaceVisible));
        }

        if (switchToBrowserRibbon)
            SelectedRibbonTab = "Browser";
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

        // Capture current URLs before they are cleared (used for resume on Idle).
        if (state == HostState.Idle)
            SaveLastKnownPosition();

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
            if (ShouldOpenParticipantWindow(participantUrl))
                _ = OpenParticipantWindowAsync(participantUrl!, presenterUrl);
            else
                AutoOpenConfiguredViews();
        }
        else if (state == HostState.Idle)
        {
            _hasAutoOpenedForCurrentRun = false;
            _sessionPlacement = null;
            _ = _presentationWindowService.CloseAsync();
            StopSessionTimer(resetElapsedOnIdle);
            ClearBrowserWorkspace();
        }
        else if (state == HostState.Error)
        {
            _sessionPlacement = null;
            _ = _presentationWindowService.CloseAsync();
            StopSessionTimer(resetElapsed: false);
        }
    }

    private async Task OpenParticipantWindowAsync(string participantUrl, string? presenterUrl)
    {
        var placementTask = _sessionPlacement ??= ResolveParticipantPlacementAsync();
        var placement = await placementTask;

        // The user may have stopped the session (or started a new one) while the dialog was open.
        if (!placement.ShouldOpen || HostState != HostState.Running || !ReferenceEquals(placementTask, _sessionPlacement))
            return;

        await _presentationWindowService.OpenAsync(participantUrl, presenterUrl, placement.Display);
    }

    private async Task<ParticipantPlacement> ResolveParticipantPlacementAsync()
    {
        var displays = _displayService.GetDisplays();
        if (displays.Count < 2)
            return new ParticipantPlacement(true, null);

        var display = await _displaySelector.SelectDisplayAsync(displays);
        return new ParticipantPlacement(display is not null, display);
    }

    private void SaveLastKnownPosition()
    {
        // Capture participant/presenter URLs before they are cleared.
        if (ParticipantUrl is not null || PresenterUrl is not null)
        {
            _lastKnownParticipantUrl = ParticipantUrl;
            _lastKnownPresenterUrl = PresenterUrl;
            HasLastKnownPosition = true;
        }
    }

    private void ClearBrowserWorkspace()
    {
        BrowserTabs.Clear();
        SelectedBrowserTab = null;
        OnPropertyChanged(nameof(HasBrowserTabs));
        OnPropertyChanged(nameof(IsBrowserSurfaceVisible));
        OnPropertyChanged(nameof(BrowserWorkspaceUri));
        if (SelectedRibbonTab == "Browser")
            SelectedRibbonTab = "Presentation";
    }

    private void TryOpenBrowser(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            ErrorMessage = $"Could not open browser: invalid address '{url}'.";
            return;
        }

        _browserLauncher.Open(uri);
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

    private static bool LooksLikeHostedUrl(string location)
    {
        if (!Uri.TryCreate(location?.Trim(), UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

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
        if (ShouldOpenParticipantWindow(participantUrl))
            _ = OpenParticipantWindowAsync(participantUrl!, PresenterUrl);
    }

    private bool ShouldOpenParticipantWindow(string? participantUrl) =>
        _settingsService.Settings.DisplayManagement.AutoDetectDisplays &&
        !string.IsNullOrWhiteSpace(participantUrl);

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

    /// <summary>Extracts the slide number from a Slidev URL fragment (e.g. <c>#/3</c> → 3).</summary>
    private static int? ExtractSlideNumber(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var fragment = uri.Fragment; // e.g. "#/3"
        if (fragment.StartsWith("#/", StringComparison.Ordinal)
            && int.TryParse(fragment[2..], out var slideNumber)
            && slideNumber >= 1)
            return slideNumber;

        return null;
    }

    private static PresentationSurfaceMode ParseSurfaceMode(string? mode) =>
        string.Equals(mode, nameof(PresentationSurfaceMode.Participant), StringComparison.OrdinalIgnoreCase)
            ? PresentationSurfaceMode.Participant
            : PresentationSurfaceMode.Presenter;

    private readonly record struct ParticipantPlacement(bool ShouldOpen, DisplayInfo? Display);

    private sealed class NullSlideDeckMetadataReader : ISlideDeckMetadataReader
    {
        public Task<SlideDeckMetadata> ReadAsync(PresentationProject project, CancellationToken cancellationToken = default) =>
            Task.FromResult(SlideDeckMetadata.Empty(project.Name));
    }

    private sealed class NullPresentationWindowService : IPresentationWindowService
    {
#pragma warning disable CS0067 // Events are never used — intentional null implementation
        public event EventHandler? PresentationExited;
        public event EventHandler<Uri>? ExternalLinkNavigated;
#pragma warning restore CS0067
        public Task OpenAsync(string participantUrl, string? presenterUrl, DisplayInfo? targetDisplay = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullDisplayService : IDisplayService
    {
        public IReadOnlyList<DisplayInfo> GetDisplays() => [];
    }

    private sealed class NullExternalBrowserLauncher : IExternalBrowserLauncher
    {
        public void Open(Uri uri) { }
    }

    /// <summary>Headless fallback: behaves like the pre-dialog logic by preferring the first non-primary display.</summary>
    private sealed class DefaultDisplaySelector : IDisplaySelector
    {
        public Task<DisplayInfo?> SelectDisplayAsync(IReadOnlyList<DisplayInfo> displays, CancellationToken cancellationToken = default) =>
            Task.FromResult<DisplayInfo?>(displays.FirstOrDefault(d => !d.IsPrimary) ?? displays.FirstOrDefault());
    }
}
