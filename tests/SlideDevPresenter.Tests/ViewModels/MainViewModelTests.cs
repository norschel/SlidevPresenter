using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Tests.ViewModels;

internal sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Settings { get; } = new();
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeSourceScanner : ISourceScanner
{
    public List<PresentationProject> Projects { get; } = [];
    public Dictionary<string, bool> IsProjectMap { get; } = [];

    public IReadOnlyList<PresentationProject> ScanRoot(string rootPath) => Projects.AsReadOnly();
    public bool IsSlidevProject(string directoryPath) =>
        IsProjectMap.TryGetValue(directoryPath, out var v) && v;
}

internal sealed class FakeSlideDeckMetadataReader : ISlideDeckMetadataReader
{
    public SlideDeckMetadata Metadata { get; set; } = SlideDeckMetadata.Empty("Presentation workspace");

    public Task<SlideDeckMetadata> ReadAsync(PresentationProject project, CancellationToken cancellationToken = default) =>
        Task.FromResult(Metadata);
}

internal sealed class FakeProcessHost : ISlidevProcessHost
{
    public HostState State { get; private set; } = HostState.Idle;
    public string? ParticipantUrl { get; private set; }
    public string? PresenterUrl { get; private set; }
    public int? Port { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }

    public event EventHandler<HostStateChangedEventArgs>? StateChanged;

    public Task StartAsync(PresentationProject project, int port, CancellationToken cancellationToken = default)
    {
        StartCallCount++;
        Port = port;
        State = HostState.Starting;
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Starting));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCallCount++;
        Port = null;
        ParticipantUrl = null;
        PresenterUrl = null;
        ErrorMessage = null;
        State = HostState.Idle;
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Idle));
        return Task.CompletedTask;
    }

    public void SimulateRunning(string participantUrl, string presenterUrl)
    {
        ParticipantUrl = participantUrl;
        PresenterUrl = presenterUrl;
        State = HostState.Running;
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Running));
    }

    public void SimulateError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        State = HostState.Error;
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Error, errorMessage));
    }
}

internal sealed class FakePresentationWindowService : IPresentationWindowService
{
    public int OpenCallCount { get; private set; }
    public int CloseCallCount { get; private set; }
    public string? LastParticipantUrl { get; private set; }
    public string? LastPresenterUrl { get; private set; }

#pragma warning disable CS0067
    public event EventHandler? PresentationExited;
    public event EventHandler<Uri>? ExternalLinkNavigated;
#pragma warning restore CS0067

    public Task OpenAsync(string participantUrl, string? presenterUrl, CancellationToken cancellationToken = default)
    {
        OpenCallCount++;
        LastParticipantUrl = participantUrl;
        LastPresenterUrl = presenterUrl;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        CloseCallCount++;
        return Task.CompletedTask;
    }

    public void SimulatePresentationExited() => PresentationExited?.Invoke(this, EventArgs.Empty);
    public void SimulateExternalLinkNavigated(Uri uri) => ExternalLinkNavigated?.Invoke(this, uri);
}

internal sealed class FakeDisplayService : IDisplayService
{
    public List<DisplayInfo> Displays { get; } = [];
    public IReadOnlyList<DisplayInfo> GetDisplays() => Displays.AsReadOnly();
}

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(
        FakeSettingsService? settings = null,
        FakeSourceScanner? scanner = null,
        FakeProcessHost? host = null,
        FakeSlideDeckMetadataReader? reader = null,
        FakePresentationWindowService? windowService = null,
        FakeDisplayService? displayService = null) =>
        new(settings ?? new(), scanner ?? new(), host ?? new(), reader ?? new(), windowService ?? new(), displayService ?? new());

    private static PresentationProject MakeProject(string name = "Talk") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        SourceType = PresentationSourceType.LocalProject,
        Location = "/tmp/" + name,
        SlidesFilePath = $"/tmp/{name}/slides.md"
    };

    [Fact]
    public void InitialState_IsIdle_AndProjectsEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal(HostState.Idle, vm.HostState);
        Assert.True(vm.IsIdle);
        Assert.False(vm.IsStarting);
        Assert.False(vm.IsRunning);
        Assert.False(vm.IsError);
        Assert.Empty(vm.Projects);
        Assert.Null(vm.SelectedProject);
    }

    [Fact]
    public async Task RefreshLibrary_PopulatesProjectsFromLocalRoot()
    {
        var scanner = new FakeSourceScanner();
        scanner.Projects.Add(MakeProject("intro"));

        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "Talks",
            Type = PresentationSourceType.LocalRoot,
            Location = "/tmp/talks",
            IsEnabled = true
        });

        var vm = CreateViewModel(settings, scanner);
        await vm.RefreshLibraryAsync();

        Assert.Single(vm.Projects);
        Assert.Equal("intro", vm.Projects[0].Name);
    }

    [Fact]
    public async Task RefreshLibrary_SkipsDisabledSources()
    {
        var scanner = new FakeSourceScanner();
        scanner.Projects.Add(MakeProject("hidden"));

        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "Disabled",
            Type = PresentationSourceType.LocalRoot,
            Location = "/tmp/hidden",
            IsEnabled = false
        });

        var vm = CreateViewModel(settings, scanner);
        await vm.RefreshLibraryAsync();

        Assert.Empty(vm.Projects);
    }

    [Fact]
    public async Task RefreshLibrary_AddsHostedUrlSources_Directly()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "Remote Talk",
            Type = PresentationSourceType.HostedUrl,
            Location = "https://slides.example.com/talk",
            IsEnabled = true
        });

        var vm = CreateViewModel(settings);
        await vm.RefreshLibraryAsync();

        Assert.Single(vm.Projects);
        Assert.Equal("Remote Talk", vm.Projects[0].Name);
        Assert.Equal("https://slides.example.com/talk", vm.Projects[0].Location);
    }

    [Fact]
    public async Task RefreshLibrary_ClearsPreviousResults()
    {
        var scanner = new FakeSourceScanner();
        scanner.Projects.Add(MakeProject("first"));

        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "Root",
            Type = PresentationSourceType.LocalRoot,
            Location = "/tmp/root",
            IsEnabled = true
        });

        var vm = CreateViewModel(settings, scanner);
        await vm.RefreshLibraryAsync();
        Assert.Single(vm.Projects);

        scanner.Projects.Clear();
        await vm.RefreshLibraryAsync();
        Assert.Empty(vm.Projects);
    }

    [Fact]
    public async Task LaunchAsync_WhenProjectSelected_CallsProcessHostStart()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        await vm.LaunchAsync();

        Assert.Equal(HostState.Starting, vm.HostState);
        Assert.True(vm.IsStarting);
        Assert.Equal(1, host.StartCallCount);
    }

    [Fact]
    public async Task LaunchAsync_UsesDefaultPortFromSettings()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Defaults.DefaultPort = 4321;

        var host = new FakeProcessHost();
        var vm = CreateViewModel(settings, host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        await vm.LaunchAsync();

        Assert.Equal(4321, host.Port);
    }

    [Fact]
    public async Task LaunchAsync_ForHostedProject_DoesNotStartLocalProcessOrUsePort()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Remote Talk",
            SourceType = PresentationSourceType.HostedUrl,
            Location = "https://slides.example.com/talk"
        });

        await vm.LaunchAsync();

        Assert.Equal(HostState.Running, vm.HostState);
        Assert.Equal(0, host.StartCallCount);
        Assert.Null(vm.Port);
        Assert.Equal("https://slides.example.com/talk", vm.ParticipantUrl);
        Assert.Equal("https://slides.example.com/talk/presenter/", vm.PresenterUrl);
    }

    [Fact]
    public async Task LaunchAsync_WithNoSelectedProject_DoesNothing()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);

        await vm.LaunchAsync();

        Assert.Equal(HostState.Idle, vm.HostState);
    }

    [Fact]
    public async Task StopAsync_TransitionsToIdle()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        await vm.StopAsync();

        Assert.Equal(HostState.Idle, vm.HostState);
        Assert.True(vm.IsIdle);
    }

    [Fact]
    public async Task StopAsync_ForHostedSession_TransitionsToIdleWithoutStoppingProcessHost()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Remote Talk",
            SourceType = PresentationSourceType.HostedUrl,
            Location = "https://slides.example.com/talk"
        });
        await vm.LaunchAsync();

        await vm.StopAsync();

        Assert.Equal(HostState.Idle, vm.HostState);
        Assert.Equal(0, host.StopCallCount);
    }

    [Fact]
    public async Task WhenHostTransitionsToRunning_ViewModelReflectsUrls()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(HostState.Running, vm.HostState);
        Assert.True(vm.IsRunning);
        Assert.Equal("http://localhost:3030/", vm.ParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/", vm.PresenterUrl);
    }

    [Fact]
    public async Task WhenHostTransitionsToError_ViewModelReflectsErrorMessage()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateError("Process exited with code 1.");

        Assert.Equal(HostState.Error, vm.HostState);
        Assert.True(vm.IsError);
        Assert.Equal("Process exited with code 1.", vm.ErrorMessage);
    }

    [Fact]
    public async Task RetryAsync_StopsAndRestarts()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateError("oops");

        await vm.RetryAsync();

        Assert.Equal(HostState.Starting, vm.HostState);
        Assert.Equal(2, host.StartCallCount);
        Assert.Equal(1, host.StopCallCount);
    }

    [Fact]
    public async Task SelectingProject_LoadsDeckMetadata()
    {
        var reader = new FakeSlideDeckMetadataReader
        {
            Metadata = new SlideDeckMetadata
            {
                DeckTitle = "Deck title",
                Slides =
                [
                    new SlideDeckSlide(1, "Intro", "Welcome"),
                    new SlideDeckSlide(2, "Agenda", "Plan")
                ]
            }
        };
        var vm = CreateViewModel(reader: reader);

        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        Assert.Equal("Deck title", vm.DeckTitle);
        Assert.Equal(2, vm.SlideCount);
        Assert.Equal("Welcome", vm.SelectedSlideSummary);
    }

    [Fact]
    public void LaunchCommand_CannotExecute_WhenNoProjectSelected()
    {
        var vm = CreateViewModel();
        Assert.False(vm.LaunchCommand.CanExecute(null));
    }

    [Fact]
    public void LaunchCommand_CannotExecute_WhenNotIdle()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.False(vm.LaunchCommand.CanExecute(null));
    }

    [Fact]
    public void StopCommand_CannotExecute_WhenIdle()
    {
        var vm = CreateViewModel();
        Assert.False(vm.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task StopCommand_CanExecute_WhenStarting()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        Assert.True(vm.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task WhenHostRunning_AndAutoDetectEnabled_OpensPresentationWindowService()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = true;

        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        displayService.Displays.Add(new DisplayInfo(1, false, 1920, 0, 1920, 1080));
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(1, windowService.OpenCallCount);
        Assert.Equal("http://localhost:3030/", windowService.LastParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/", windowService.LastPresenterUrl);
    }

    [Fact]
    public async Task WhenHostRunning_AndAutoDetectDisabled_DoesNotOpenPresentationWindowService()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = false;

        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        displayService.Displays.Add(new DisplayInfo(1, false, 1920, 0, 1920, 1080));
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(0, windowService.OpenCallCount);
    }

    [Fact]
    public async Task WhenHostRunning_WithSingleDisplay_OpensPresentationWindowService()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = true;

        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(1, windowService.OpenCallCount);
        Assert.Equal("http://localhost:3030/", windowService.LastParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/", windowService.LastPresenterUrl);
    }

    [Fact]
    public async Task WhenHostStops_ClosesPresentationWindowService()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = true;

        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        displayService.Displays.Add(new DisplayInfo(1, false, 1920, 0, 1920, 1080));
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        await vm.StopAsync();

        Assert.Equal(1, windowService.CloseCallCount);
    }

    [Fact]
    public async Task WhenHostErrors_ClosesPresentationWindowService()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = true;

        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        displayService.Displays.Add(new DisplayInfo(1, false, 1920, 0, 1920, 1080));
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        host.SimulateError("crash");

        Assert.Equal(1, windowService.CloseCallCount);
    }

    [Fact]
    public async Task WhenPresentationExitedFired_StopsPresentation()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = true;

        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        displayService.Displays.Add(new DisplayInfo(1, false, 1920, 0, 1920, 1080));
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        windowService.SimulatePresentationExited();

        Assert.Equal(HostState.Idle, vm.HostState);
        Assert.Equal(1, host.StopCallCount);
    }

    [Fact]
    public async Task StartFromBeginningAsync_NavigatesToFirstSlide()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        await vm.StartFromBeginningAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal("http://localhost:3030/#/1", vm.ParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/#/1", vm.PresenterUrl);
    }

    [Fact]
    public async Task StartFromCurrentSlideAsync_NavigatesToSelectedSlide()
    {
        var host = new FakeProcessHost();
        var reader = new FakeSlideDeckMetadataReader
        {
            Metadata = new SlideDeckMetadata
            {
                DeckTitle = "Deck",
                Slides =
                [
                    new SlideDeckSlide(1, "Intro", "Welcome"),
                    new SlideDeckSlide(7, "Demo", "Current")
                ]
            }
        };
        var vm = CreateViewModel(host: host, reader: reader);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        vm.SelectedOutlineSlide = new SlideDeckSlide(7, "Demo", "Current");

        await vm.StartFromCurrentSlideAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal("http://localhost:3030/#/7", vm.ParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/#/7", vm.PresenterUrl);
    }

    [Fact]
    public async Task StartPresenterViewAsync_SelectsPresenterSurface()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        vm.UseParticipantSurface();

        await vm.StartPresenterViewAsync();

        Assert.Equal(PresentationSurfaceMode.Presenter, vm.SelectedSurfaceMode);
    }

    [Fact]
    public void SelectPresentationRibbon_SetsIsPresentationRibbonSelected()
    {
        var vm = CreateViewModel();
        vm.SelectPresentationRibbon();
        Assert.True(vm.IsPresentationRibbonSelected);
        Assert.False(vm.IsHomeRibbonSelected);
    }

    [Fact]
    public void DetectDisplays_UpdatesDetectedDisplayCount()
    {
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        displayService.Displays.Add(new DisplayInfo(1, false, 1920, 0, 1920, 1080));

        var vm = CreateViewModel(displayService: displayService);
        vm.DetectDisplays();

        Assert.Equal(2, vm.DetectedDisplayCount);
        Assert.True(vm.HasDetectedDisplays);
    }

    [Fact]
    public void DetectDisplays_WhenNoDisplaysReported_HasDetectedDisplaysIsFalse()
    {
        var vm = CreateViewModel();
        vm.DetectDisplays();

        Assert.Equal(0, vm.DetectedDisplayCount);
        Assert.False(vm.HasDetectedDisplays);
    }

    // BUG-007: Embedded browser workspace tests

    [Fact]
    public void OpenInEmbeddedBrowser_AddsTab_AndSwitchesToBrowserRibbon()
    {
        var vm = CreateViewModel();
        var uri = new Uri("https://github.com");

        vm.OpenInEmbeddedBrowser(uri);

        Assert.Single(vm.BrowserTabs);
        Assert.Equal(uri, vm.BrowserTabs[0].Url);
        Assert.Equal("Browser", vm.SelectedRibbonTab);
        Assert.True(vm.IsBrowserRibbonSelected);
        Assert.False(vm.IsNormalWorkspaceVisible);
    }

    [Fact]
    public void OpenInEmbeddedBrowser_SameUrl_DoesNotAddDuplicateTab()
    {
        var vm = CreateViewModel();
        var uri = new Uri("https://github.com");

        vm.OpenInEmbeddedBrowser(uri);
        vm.OpenInEmbeddedBrowser(uri);

        Assert.Single(vm.BrowserTabs);
    }

    [Fact]
    public void OpenInEmbeddedBrowser_DifferentUrls_AddsMultipleTabs()
    {
        var vm = CreateViewModel();

        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));
        vm.OpenInEmbeddedBrowser(new Uri("https://docs.microsoft.com"));

        Assert.Equal(2, vm.BrowserTabs.Count);
    }

    [Fact]
    public void CloseBrowserTab_RemovesTab_AndSelectsLast()
    {
        var vm = CreateViewModel();
        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));
        vm.OpenInEmbeddedBrowser(new Uri("https://docs.microsoft.com"));
        var firstTab = vm.BrowserTabs[0];
        var secondTab = vm.BrowserTabs[1];

        vm.CloseBrowserTab(firstTab);

        Assert.Single(vm.BrowserTabs);
        Assert.Equal(secondTab, vm.BrowserTabs[0]);
    }

    [Fact]
    public void CloseBrowserTab_WhenLastTab_SelectedBrowserTabBecomesNull()
    {
        var vm = CreateViewModel();
        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));
        var tab = vm.BrowserTabs[0];

        vm.CloseBrowserTab(tab);

        Assert.Empty(vm.BrowserTabs);
        Assert.Null(vm.SelectedBrowserTab);
        Assert.False(vm.HasBrowserTabs);
    }

    [Fact]
    public void BrowserWorkspaceUri_WhenNoTabsSelected_ReturnsAboutBlank()
    {
        var vm = CreateViewModel();

        Assert.Equal(new Uri("about:blank"), vm.BrowserWorkspaceUri);
    }

    [Fact]
    public void BrowserWorkspaceUri_WhenTabSelected_ReturnsTabUrl()
    {
        var vm = CreateViewModel();
        var uri = new Uri("https://github.com");
        vm.OpenInEmbeddedBrowser(uri);

        Assert.Equal(uri, vm.BrowserWorkspaceUri);
    }

    [Fact]
    public void SelectBrowserRibbon_SetsBrowserRibbonSelected()
    {
        var vm = CreateViewModel();

        vm.SelectBrowserRibbon();

        Assert.True(vm.IsBrowserRibbonSelected);
        Assert.False(vm.IsNormalWorkspaceVisible);
        Assert.False(vm.IsHomeRibbonSelected);
    }

    [Fact]
    public void IsNormalWorkspaceVisible_WhenBrowserTabSelected_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.SelectBrowserRibbon();

        Assert.False(vm.IsNormalWorkspaceVisible);
    }

    [Fact]
    public void IsNormalWorkspaceVisible_WhenNonBrowserTabSelected_ReturnsTrue()
    {
        var vm = CreateViewModel();
        vm.SelectHomeRibbon();

        Assert.True(vm.IsNormalWorkspaceVisible);
    }

    [Fact]
    public void OpenInEmbeddedBrowser_FromPresentationExternalLink_DoesNotSwitchRibbonTab()
    {
        var windowService = new FakePresentationWindowService();
        var vm = CreateViewModel(windowService: windowService);
        vm.SelectPresentationRibbon();

        windowService.SimulateExternalLinkNavigated(new Uri("https://github.com"));

        Assert.Single(vm.BrowserTabs);
        Assert.Equal("Presentation", vm.SelectedRibbonTab);
        Assert.False(vm.IsBrowserRibbonSelected);
    }

    [Fact]
    public void OpenInEmbeddedBrowser_WhenEmbeddedBrowserDisabled_DoesNotOpenTab()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Navigation.OpenExternalLinksInEmbeddedBrowser = false;
        var windowService = new FakePresentationWindowService();
        var vm = CreateViewModel(settings: settings, windowService: windowService);

        // Simulate the external link navigated event from the presentation window service
        windowService.SimulateExternalLinkNavigated(new Uri("https://github.com"));

        Assert.Empty(vm.BrowserTabs);
    }

    // BUG-ESC: Stopping via ESC should clear browser tabs and reset ribbon

    [Fact]
    public async Task StopAsync_ClearsBrowserTabs()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");
        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));
        vm.OpenInEmbeddedBrowser(new Uri("https://docs.microsoft.com"));
        Assert.Equal(2, vm.BrowserTabs.Count);

        await vm.StopAsync();

        Assert.Empty(vm.BrowserTabs);
        Assert.Null(vm.SelectedBrowserTab);
        Assert.False(vm.HasBrowserTabs);
    }

    [Fact]
    public async Task StopAsync_WhenBrowserRibbonSelected_SwitchesToPresentationRibbon()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");
        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));
        Assert.Equal("Browser", vm.SelectedRibbonTab);

        await vm.StopAsync();

        Assert.Equal("Presentation", vm.SelectedRibbonTab);
        Assert.False(vm.IsBrowserRibbonSelected);
        Assert.True(vm.IsNormalWorkspaceVisible);
    }

    [Fact]
    public async Task WhenParticipantWindowClosedByUser_ClearsBrowserTabsAndSwitchesRibbon()
    {
        // Simulates the user closing the participant window via the X button (or OS close),
        // which fires PresentationExited — same event path as CloseOnUiThread in
        // PresentationWindowService after the Closed event subscription was added.
        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var vm = CreateViewModel(host: host, windowService: windowService);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");
        vm.OpenInEmbeddedBrowser(new Uri("https://example.com/slide1"));
        vm.OpenInEmbeddedBrowser(new Uri("https://example.com/slide2"));
        Assert.Equal(2, vm.BrowserTabs.Count);
        Assert.Equal("Browser", vm.SelectedRibbonTab);

        // Fires PresentationExited — this now also happens when the window is closed via X.
        windowService.SimulatePresentationExited();

        Assert.Empty(vm.BrowserTabs);
        Assert.Equal("Presentation", vm.SelectedRibbonTab);
        Assert.False(vm.IsBrowserRibbonSelected);
    }

    [Fact]
    public async Task StopAsync_SavesLastKnownPosition()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.StartFromBeginningAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");
        // Participant URL is http://localhost:3030/#/1

        await vm.StopAsync();

        Assert.True(vm.HasLastKnownPosition);
    }

    [Fact]
    public async Task StopAsync_DoesNotSavePosition_WhenNoUrlsWereEverSet()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        // Stop without ever running (edge case — HostState never reached Running)
        // Trigger ApplyStateSnapshot(Idle) directly via a fresh stop
        host.SimulateError("startup failure");
        await vm.StopAsync(); // stops from Error state via process host
        // ParticipantUrl was never set, so HasLastKnownPosition should remain false
        Assert.False(vm.HasLastKnownPosition);
    }

    [Fact]
    public async Task ResumeAsync_RestartsFromLastSlide()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.StartFromBeginningAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");
        // Navigate to slide 5
        vm.SelectedOutlineSlide = new SlideDeckSlide(5, "Chapter", "Content");
        await vm.StartFromCurrentSlideAsync();
        // Now at slide 5; participant URL = http://localhost:3030/#/5
        await vm.StopAsync();

        Assert.True(vm.HasLastKnownPosition);
        Assert.True(vm.ResumeCommand.CanExecute(null));

        await vm.ResumeAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal("http://localhost:3030/#/5", vm.ParticipantUrl);
        Assert.Equal("http://localhost:3030/presenter/#/5", vm.PresenterUrl);
    }

    [Fact]
    public async Task ResumeCommand_CannotExecute_WhenRunning()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.StartFromBeginningAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.False(vm.ResumeCommand.CanExecute(null));
    }

    [Fact]
    public void ResumeCommand_CannotExecute_WhenNoSavedPosition()
    {
        var vm = CreateViewModel();
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        Assert.False(vm.HasLastKnownPosition);
        Assert.False(vm.ResumeCommand.CanExecute(null));
    }
}
