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

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(
        FakeSettingsService? settings = null,
        FakeSourceScanner? scanner = null,
        FakeProcessHost? host = null,
        FakeSlideDeckMetadataReader? reader = null) =>
        new(settings ?? new(), scanner ?? new(), host ?? new(), reader ?? new());

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
}
