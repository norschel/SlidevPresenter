using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;
using System.Text.Json;

namespace SlideDevPresenter.Tests.ViewModels;

internal sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Settings { get; private set; } = new();
    public int SaveCount { get; private set; }
    public Exception? SaveException { get; set; }
    public Task? SaveTask { get; set; }
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        if (SaveException is not null)
            return Task.FromException(SaveException);
        if (SaveTask is not null)
            return SaveTask;
        return Task.CompletedTask;
    }
    public Task ExportAsync(Stream destination, CancellationToken cancellationToken = default) =>
        JsonSerializer.SerializeAsync(destination, Settings, cancellationToken: cancellationToken);

    public async Task ImportAsync(Stream source, CancellationToken cancellationToken = default) =>
        Settings = await JsonSerializer.DeserializeAsync<AppSettings>(source, cancellationToken: cancellationToken)
                   ?? new AppSettings();
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
    public DisplayInfo? LastTargetDisplay { get; private set; }

#pragma warning disable CS0067
    public event EventHandler? PresentationExited;
    public event EventHandler<Uri>? ExternalLinkNavigated;
#pragma warning restore CS0067

    public Task OpenAsync(string participantUrl, string? presenterUrl, DisplayInfo? targetDisplay = null, CancellationToken cancellationToken = default)
    {
        OpenCallCount++;
        LastParticipantUrl = participantUrl;
        LastPresenterUrl = presenterUrl;
        LastTargetDisplay = targetDisplay;
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

internal sealed class FakeDisplaySelector : IDisplaySelector
{
    public int CallCount { get; private set; }
    public IReadOnlyList<DisplayInfo>? OfferedDisplays { get; private set; }
    public DisplayInfo? Result { get; set; }
    public TaskCompletionSource<DisplayInfo?>? PendingResult { get; set; }

    public Task<DisplayInfo?> SelectDisplayAsync(IReadOnlyList<DisplayInfo> displays, CancellationToken cancellationToken = default)
    {
        CallCount++;
        OfferedDisplays = displays;
        return PendingResult?.Task ?? Task.FromResult(Result);
    }
}

internal sealed class FakeSlideDeckPdfExporter : ISlideDeckPdfExporter
{
    public PresentationProject? ExportedProject { get; private set; }
    public string? OutputPath { get; private set; }
    public Exception? ExceptionToThrow { get; set; }
    public Task? ExportTask { get; set; }

    public Task ExportPdfAsync(PresentationProject project, string outputPath, CancellationToken cancellationToken = default)
    {
        ExportedProject = project;
        OutputPath = outputPath;
        return ExportTask ?? (ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow));
    }
}

internal sealed class FakeGitRepositoryService : IGitRepositoryService
{
    public bool Available { get; set; } = true;
    public RepositoryPreparation Result { get; set; } = new("/tmp/checkout", null);
    public int PrepareCount { get; private set; }
    public string? PreparedBranch { get; private set; }
    public bool AskForUpdate { get; set; }
    public bool? UpdateAccepted { get; private set; }

    public Task<bool> IsGitAvailableAsync() => Task.FromResult(Available);
    public Task<IReadOnlyList<string>> GetBranchesAsync(PresentationSource source) => Task.FromResult<IReadOnlyList<string>>(["main", "release/slides"]);
    public string? CurrentBranch { get; set; } = "main";
    public Task<string?> GetCurrentBranchAsync(PresentationSource source) => Task.FromResult(Available ? CurrentBranch : null);

    public async Task<RepositoryPreparation> PrepareAsync(PresentationSource source, Func<string, Task<bool>> confirmUpdate)
    {
        PrepareCount++;
        PreparedBranch = source.Branch;
        if (AskForUpdate)
            UpdateAccepted = await confirmUpdate("New commits available");
        return Result;
    }
}

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(
        FakeSettingsService? settings = null,
        FakeSourceScanner? scanner = null,
        FakeProcessHost? host = null,
        FakeSlideDeckMetadataReader? reader = null,
        FakePresentationWindowService? windowService = null,
        FakeDisplayService? displayService = null,
        FakeSlideDeckPdfExporter? pdfExporter = null,
        FakeDisplaySelector? displaySelector = null,
        FakeGitRepositoryService? gitService = null) =>
        new(settings ?? new(), scanner ?? new(), host ?? new(), reader ?? new(), windowService ?? new(), displayService ?? new(), pdfExporter, displaySelector, gitService);

    [Theory]
    [InlineData(PresentationSourceType.Git)]
    [InlineData(PresentationSourceType.GitHub)]
    public async Task RefreshLibrary_RepositoryHttpUrl_PreparesCheckoutInsteadOfHostingWebPage(PresentationSourceType type)
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository slides", Type = type, Location = "https://github.com/owner/slides"
        });
        var scanner = new FakeSourceScanner();
        scanner.Projects.Add(new PresentationProject
        {
            Id = Guid.NewGuid(), Name = "checkout", SourceType = PresentationSourceType.LocalProject,
            Location = "/tmp/checkout", SlidesFilePath = "/tmp/checkout/slides.md"
        });
        var git = new FakeGitRepositoryService { AskForUpdate = true };
        var vm = CreateViewModel(settings, scanner, gitService: git);
        vm.ConfirmRepositoryUpdateAsync = _ => Task.FromResult(false);

        await vm.RefreshLibraryAsync();
        var project = Assert.Single(vm.Projects);
        Assert.Equal(PresentationSourceType.LocalProject, project.SourceType);
        Assert.Equal("/tmp/checkout", project.Location);
        Assert.Equal("Repository slides", project.Name);
        Assert.False(git.UpdateAccepted);
        var id = project.Id;
        await vm.RefreshLibraryAsync();
        Assert.Equal(id, Assert.Single(vm.Projects).Id);
        Assert.Equal(id, vm.SelectedProject?.Id);
    }

    [Fact]
    public async Task SelectedRepositoryProject_CanPersistAndRefreshWithAnotherBranch()
    {
        var settings = new FakeSettingsService();
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git,
            Location = "https://github.com/owner/slides", Branch = "main"
        };
        settings.Settings.Sources.Add(source);
        var git = new FakeGitRepositoryService();
        var vm = CreateViewModel(settings, gitService: git);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject(), source.Id);

        Assert.True(vm.IsRepositoryProjectSelected);
        Assert.Equal(new[] { "main", "release/slides" }, vm.AvailableBranches);
        Assert.True(vm.CanChangeSelectedBranch);

        await vm.ChangeSelectedBranchAsync("release/slides");

        Assert.Equal("release/slides", source.Branch);
        Assert.Equal("release/slides", git.PreparedBranch);
        Assert.Equal(1, settings.SaveCount);
        Assert.Equal(1, git.PrepareCount);
    }

    [Theory]
    [InlineData("main", true)]
    [InlineData(null, false)]
    public void SelectedLocalProject_DetectsGitRepositoryAndOffersBranches(string? currentBranch, bool isRepository)
    {
        var settings = new FakeSettingsService();
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Local", Type = PresentationSourceType.LocalRoot, Location = "/tmp/slides"
        };
        settings.Settings.Sources.Add(source);
        var git = new FakeGitRepositoryService { CurrentBranch = currentBranch };
        var vm = CreateViewModel(settings, gitService: git);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject(), source.Id);

        Assert.Equal(isRepository, vm.IsRepositoryProjectSelected);
        Assert.Equal(isRepository, vm.CanChangeSelectedBranch);
        Assert.Equal(currentBranch, vm.SelectedBranch);
    }

    [Fact]
    public async Task SelectedLocalProject_ChangingBranch_PreparesLocalFolder()
    {
        var settings = new FakeSettingsService();
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Local", Type = PresentationSourceType.LocalRoot, Location = "/tmp/slides"
        };
        settings.Settings.Sources.Add(source);
        var git = new FakeGitRepositoryService();
        var vm = CreateViewModel(settings, gitService: git);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject(), source.Id);

        await vm.ChangeSelectedBranchAsync("release/slides");

        Assert.Equal("release/slides", source.Branch);
        Assert.Equal("release/slides", git.PreparedBranch);
        Assert.Equal(1, git.PrepareCount);
    }

    [Fact]
    public async Task SelectedRepositoryProject_SaveFailure_RestoresBranchWithoutRefreshing()
    {
        var settings = new FakeSettingsService { SaveException = new IOException("Disk full") };
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git,
            Location = "https://github.com/owner/slides", Branch = "main"
        };
        settings.Settings.Sources.Add(source);
        var git = new FakeGitRepositoryService();
        var vm = CreateViewModel(settings, gitService: git);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject(), source.Id);

        await vm.ChangeSelectedBranchAsync("release/slides");

        Assert.Equal("main", source.Branch);
        Assert.Equal("main", vm.SelectedBranch);
        Assert.Equal(1, settings.SaveCount);
        Assert.Equal(0, git.PrepareCount);
        Assert.False(vm.IsRefreshing);
        Assert.Contains("Could not save the selected branch", vm.SourceMessage);
    }

    [Fact]
    public async Task SelectedRepositoryProject_DeclinedSwitch_RestoresBranchAndReloadsCheckout()
    {
        var settings = new FakeSettingsService();
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git,
            Location = "https://github.com/owner/slides", Branch = "main"
        };
        settings.Settings.Sources.Add(source);
        var scanner = new FakeSourceScanner();
        scanner.Projects.Add(new PresentationProject
        {
            Id = Guid.NewGuid(), Name = "Checkout", SourceType = PresentationSourceType.LocalProject,
            Location = "/tmp/checkout", SlidesFilePath = "/tmp/checkout/slides.md"
        });
        var git = new FakeGitRepositoryService
        {
            Result = new("/tmp/checkout", "Branch switch declined.", BranchSwitchDeclined: true)
        };
        var vm = CreateViewModel(settings, scanner, gitService: git);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject(), source.Id);

        await vm.ChangeSelectedBranchAsync("release/slides");

        Assert.Equal("main", source.Branch);
        Assert.Equal("main", vm.SelectedBranch);
        Assert.Equal(2, settings.SaveCount);
        Assert.Equal("/tmp/checkout", Assert.Single(vm.Projects).Location);
        Assert.Contains("Branch switch declined", vm.SourceMessage);
        Assert.False(vm.IsRefreshing);
    }

    [Fact]
    public async Task SelectedRepositoryProject_SaveInProgress_DisablesBranchChangeAndLaunch()
    {
        var settings = new FakeSettingsService();
        var source = new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git,
            Location = "https://github.com/owner/slides", Branch = "main"
        };
        settings.Settings.Sources.Add(source);
        var vm = CreateViewModel(settings, gitService: new FakeGitRepositoryService());
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject(), source.Id);
        var savePending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        settings.SaveTask = savePending.Task;

        var changeTask = vm.ChangeSelectedBranchAsync("release/slides");

        Assert.True(vm.IsRefreshing);
        Assert.False(vm.CanChangeSelectedBranch);
        Assert.False(vm.LaunchCommand.CanExecute(null));

        savePending.SetResult();
        await changeTask;
        Assert.False(vm.IsRefreshing);
    }

    [Fact]
    public async Task RefreshLibrary_MissingGit_SkipsRepositoryButKeepsHostedSources()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git, Location = "https://github.com/owner/slides"
        });
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Hosted", Type = PresentationSourceType.HostedUrl, Location = "http://slides.example.com"
        });
        var git = new FakeGitRepositoryService { Available = false };
        var vm = CreateViewModel(settings, gitService: git);

        await vm.RefreshLibraryAsync();

        Assert.Equal("Hosted", Assert.Single(vm.Projects).Name);
        Assert.Equal(0, git.PrepareCount);
        Assert.Contains("Install Git", vm.SourceMessage);
        Assert.True(settings.Settings.Sources[0].IsEnabled);
    }

    [Fact]
    public async Task RefreshLibrary_PendingClone_ShowsInstructionsWithoutLaunchingPresentation()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git, Location = "https://github.com/owner/slides"
        });
        var git = new FakeGitRepositoryService { Result = new(null, "Finish cloning in terminal, then refresh.") };
        var host = new FakeProcessHost();
        var vm = CreateViewModel(settings, host: host, gitService: git);

        await vm.RefreshLibraryAsync();

        Assert.Empty(vm.Projects);
        Assert.Contains("Finish cloning", vm.SourceMessage);
        Assert.Equal(0, host.StartCallCount);
        Assert.False(vm.IsRefreshing);
    }

    [Theory]
    [InlineData(HostState.Starting)]
    [InlineData(HostState.Running)]
    public async Task RefreshLibrary_ActivePresentation_DoesNotSwitchRepositoryBranch(HostState state)
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git, Location = "https://github.com/owner/slides"
        });
        var git = new FakeGitRepositoryService();
        var vm = CreateViewModel(settings, gitService: git);
        vm.HostState = state;

        await vm.RefreshLibraryAsync();

        Assert.Equal(0, git.PrepareCount);
        Assert.Contains("Stop the presentation", vm.SourceMessage);
    }

    [Fact]
    public async Task LaunchAsync_RefreshInProgress_DoesNotStartPresentation()
    {
        var host = new FakeProcessHost();
        var vm = CreateViewModel(host: host);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        vm.IsRefreshing = true;

        await vm.LaunchAsync();

        Assert.False(vm.LaunchCommand.CanExecute(null));
        Assert.Equal(0, host.StartCallCount);
        Assert.Contains("Wait for the source refresh", vm.SourceMessage);
    }

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

    [Theory]
    [InlineData(HostState.Idle, false)]
    [InlineData(HostState.Starting, false)]
    [InlineData(HostState.Running, true)]
    [InlineData(HostState.Error, false)]
    public void IsRunning_ForNavigationVisibility_NotifiesOnStateChange(HostState state, bool expected)
    {
        var vm = CreateViewModel();
        vm.HostState = state == HostState.Running ? HostState.Idle : HostState.Running;
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.HostState = state;

        Assert.Equal(expected, vm.IsRunning);
        Assert.Contains(nameof(MainViewModel.IsRunning), changedProperties);
    }

    [Fact]
    public async Task ExportPdf_UsesSelectedLocalProjectAndUpdatesMessage()
    {
        var pdfExporter = new FakeSlideDeckPdfExporter();
        var vm = CreateViewModel(pdfExporter: pdfExporter);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        await vm.ExportPdfAsync("/tmp/talk.pdf");

        Assert.NotNull(pdfExporter.ExportedProject);
        Assert.Equal("Talk", pdfExporter.ExportedProject.Name);
        Assert.Equal("/tmp/talk.pdf", pdfExporter.OutputPath);
        Assert.Equal("PDF exported to talk.pdf.", vm.PdfExportMessage);
        Assert.False(vm.IsExportingPdf);
    }

    [Fact]
    public async Task ExportPdf_ShowsProgressMessageWhileExporterIsRunning()
    {
        var exportCompletion = new TaskCompletionSource();
        var pdfExporter = new FakeSlideDeckPdfExporter { ExportTask = exportCompletion.Task };
        var vm = CreateViewModel(pdfExporter: pdfExporter);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        var exportTask = vm.ExportPdfAsync("/tmp/talk.pdf");

        Assert.True(vm.IsExportingPdf);
        Assert.Equal("Exporting PDF...", vm.PdfExportMessage);

        exportCompletion.SetResult();
        await exportTask;
        Assert.Equal("PDF exported to talk.pdf.", vm.PdfExportMessage);
    }

    [Fact]
    public async Task ExportPdf_ReportsExporterErrors()
    {
        var pdfExporter = new FakeSlideDeckPdfExporter { ExceptionToThrow = new InvalidOperationException("npx not found") };
        var vm = CreateViewModel(pdfExporter: pdfExporter);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());

        await vm.ExportPdfAsync("/tmp/talk.pdf");

        Assert.Equal("PDF export failed: npx not found", vm.PdfExportMessage);
        Assert.False(vm.IsExportingPdf);
    }

    [Fact]
    public async Task ExportPdf_IsUnavailableForHostedProjects()
    {
        var pdfExporter = new FakeSlideDeckPdfExporter();
        var vm = CreateViewModel(pdfExporter: pdfExporter);
        vm.SelectedProject = new PresentationProjectViewModel(new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Hosted Talk",
            SourceType = PresentationSourceType.HostedUrl,
            Location = "https://slides.example.com"
        });

        Assert.False(vm.CanExportPdf);
        await vm.ExportPdfAsync("/tmp/talk.pdf");
        Assert.Null(pdfExporter.ExportedProject);
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
    public async Task RefreshLibrary_TreatsHttpLocationAsHostedUrl_WhenSavedAsLocalRoot()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "Remote From Root",
            Type = PresentationSourceType.LocalRoot,
            Location = "https://slides.example.com/talk",
            IsEnabled = true
        });

        var vm = CreateViewModel(settings);
        await vm.RefreshLibraryAsync();

        Assert.Single(vm.Projects);
        Assert.Equal("Remote From Root", vm.Projects[0].Name);
        Assert.Equal(PresentationSourceType.HostedUrl, vm.Projects[0].SourceType);
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
    public void IsBrowserSurfaceVisible_WhenBrowserRibbonSelectedAndHasTabs_ReturnsTrue()
    {
        var vm = CreateViewModel();
        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));

        Assert.True(vm.IsBrowserRibbonSelected);
        Assert.True(vm.IsBrowserSurfaceVisible);
    }

    [Fact]
    public void IsBrowserSurfaceVisible_WhenSwitchingAwayFromBrowserRibbon_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));
        Assert.True(vm.IsBrowserSurfaceVisible);

        vm.SelectHomeRibbon();

        Assert.True(vm.HasBrowserTabs);
        Assert.False(vm.IsBrowserSurfaceVisible);
    }

    [Fact]
    public void IsBrowserSurfaceVisible_WhenBrowserRibbonSelectedButNoTabs_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.SelectBrowserRibbon();

        Assert.True(vm.IsBrowserRibbonSelected);
        Assert.False(vm.HasBrowserTabs);
        Assert.False(vm.IsBrowserSurfaceVisible);
    }

    [Fact]
    public void OpenInEmbeddedBrowser_RaisesIsBrowserSurfaceVisibleChange()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBrowserSurfaceVisible))
                raised = true;
        };

        vm.OpenInEmbeddedBrowser(new Uri("https://github.com"));

        Assert.True(raised);
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

    // ---- Display selection on presentation start ----

    private static (FakeSettingsService settings, FakeProcessHost host, FakePresentationWindowService windowService, FakeDisplayService displayService) MakeMultiDisplaySetup()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = true;
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        displayService.Displays.Add(new DisplayInfo(1, false, 1920, 0, 2560, 1440));
        displayService.Displays.Add(new DisplayInfo(2, false, 4480, 0, 1920, 1080));
        return (settings, new FakeProcessHost(), new FakePresentationWindowService(), displayService);
    }

    [Fact]
    public async Task WhenHostRunning_WithMultipleDisplays_AsksSelectorAndOpensOnChosenDisplay()
    {
        var (settings, host, windowService, displayService) = MakeMultiDisplaySetup();
        var selector = new FakeDisplaySelector { Result = displayService.Displays[2] };
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService, displaySelector: selector);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(1, selector.CallCount);
        Assert.Equal(3, selector.OfferedDisplays!.Count);
        Assert.Equal(1, windowService.OpenCallCount);
        Assert.Equal(displayService.Displays[2], windowService.LastTargetDisplay);
    }

    [Fact]
    public async Task WhenHostRunning_WithSingleDisplay_DoesNotAskSelector()
    {
        var settings = new FakeSettingsService();
        settings.Settings.DisplayManagement.AutoDetectDisplays = true;
        var displayService = new FakeDisplayService();
        displayService.Displays.Add(new DisplayInfo(0, true, 0, 0, 1920, 1080));
        var host = new FakeProcessHost();
        var windowService = new FakePresentationWindowService();
        var selector = new FakeDisplaySelector { Result = displayService.Displays[0] };
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService, displaySelector: selector);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(0, selector.CallCount);
        Assert.Equal(1, windowService.OpenCallCount);
        Assert.Null(windowService.LastTargetDisplay);
    }

    [Fact]
    public async Task WhenSelectorDeclines_DoesNotOpenParticipantWindow()
    {
        var (settings, host, windowService, displayService) = MakeMultiDisplaySetup();
        var selector = new FakeDisplaySelector { Result = null };
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService, displaySelector: selector);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();

        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(1, selector.CallCount);
        Assert.Equal(0, windowService.OpenCallCount);
        Assert.Equal(HostState.Running, vm.HostState);
    }

    [Fact]
    public async Task NavigatingWithinRunningSession_ReusesDisplayChoice_WithoutAskingAgain()
    {
        var (settings, host, windowService, displayService) = MakeMultiDisplaySetup();
        var selector = new FakeDisplaySelector { Result = displayService.Displays[1] };
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService, displaySelector: selector);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        await vm.StartFromBeginningAsync();

        Assert.Equal(1, selector.CallCount);
        Assert.Equal(2, windowService.OpenCallCount);
        Assert.Equal(displayService.Displays[1], windowService.LastTargetDisplay);
    }

    [Fact]
    public async Task RestartingSession_AsksForDisplayAgain()
    {
        var (settings, host, windowService, displayService) = MakeMultiDisplaySetup();
        var selector = new FakeDisplaySelector { Result = displayService.Displays[1] };
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService, displaySelector: selector);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");
        await vm.StopAsync();

        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");

        Assert.Equal(2, selector.CallCount);
        Assert.Equal(2, windowService.OpenCallCount);
    }

    [Fact]
    public async Task WhenSessionStopsWhileSelectorIsOpen_DoesNotOpenParticipantWindow()
    {
        var (settings, host, windowService, displayService) = MakeMultiDisplaySetup();
        var selector = new FakeDisplaySelector { PendingResult = new TaskCompletionSource<DisplayInfo?>() };
        var vm = CreateViewModel(settings, host: host, windowService: windowService, displayService: displayService, displaySelector: selector);
        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        await vm.LaunchAsync();
        host.SimulateRunning("http://localhost:3030/", "http://localhost:3030/presenter/");
        Assert.Equal(0, windowService.OpenCallCount);

        await vm.StopAsync();
        selector.PendingResult.SetResult(displayService.Displays[1]);
        await Task.Yield();

        Assert.Equal(0, windowService.OpenCallCount);
    }

    // ---- Library import / export ----

    private static PresentationSource MakeHostedSource(string name, string url) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Type = PresentationSourceType.HostedUrl,
        Location = url
    };

    [Fact]
    public async Task ExportLibrary_WritesHostedSourcesAsSourceList()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(MakeHostedSource("Talk", "https://slides.example.com/talk/"));
        var vm = CreateViewModel(settings);

        using var stream = new MemoryStream();
        await vm.ExportLibraryAsync(stream);
        stream.Position = 0;

        var reimported = await PresentationSourceListSerializer.ImportAsync(stream);
        var single = Assert.Single(reimported);
        Assert.Equal("Talk", single.Name);
        Assert.Equal("https://slides.example.com/talk/", single.Location);
        Assert.Equal("Library exported.", vm.LibraryMessage);
    }

    [Fact]
    public async Task ImportLibrary_AddsNewSources_SkipsDuplicates_AndRefreshes()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(MakeHostedSource("Existing", "https://slides.example.com/existing/"));
        var vm = CreateViewModel(settings);

        using var exportStream = new MemoryStream();
        await PresentationSourceListSerializer.ExportAsync(
        [
            MakeHostedSource("Existing", "https://slides.example.com/EXISTING/"),
            MakeHostedSource("New", "https://slides.example.com/new/")
        ], exportStream);
        exportStream.Position = 0;

        await vm.ImportLibraryAsync(exportStream);

        Assert.Equal(2, settings.Settings.Sources.Count);
        Assert.Contains(settings.Settings.Sources, s => s.Name == "New");
        Assert.Equal(2, vm.Projects.Count);
        Assert.Contains("Imported 1", vm.LibraryMessage);
        Assert.Contains("skipped 1", vm.LibraryMessage);
    }

    [Fact]
    public async Task ImportLibrary_WithInvalidJson_Throws()
    {
        var vm = CreateViewModel();
        using var stream = new MemoryStream("not json"u8.ToArray());

        await Assert.ThrowsAnyAsync<Exception>(() => vm.ImportLibraryAsync(stream));
        Assert.Empty(vm.Projects);
    }

    // ---- Library context menu commands ----

    [Fact]
    public async Task RemoveSelectedProjectFromLibrary_RemovesMatchingSource()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(MakeHostedSource("Keep", "https://slides.example.com/keep/"));
        settings.Settings.Sources.Add(MakeHostedSource("Drop", "https://slides.example.com/drop/"));
        var vm = CreateViewModel(settings);
        await vm.RefreshLibraryAsync();
        vm.SelectedProject = vm.Projects.Single(p => p.Name == "Drop");

        Assert.True(vm.RemoveSelectedProjectFromLibraryCommand.CanExecute(null));
        await vm.RemoveSelectedProjectFromLibraryAsync();

        var remaining = Assert.Single(settings.Settings.Sources);
        Assert.Equal("Keep", remaining.Name);
        Assert.Single(vm.Projects);
        Assert.Contains("Drop", vm.LibraryMessage);
    }

    [Fact]
    public async Task RemoveSelectedProjectFromLibrary_CannotExecute_ForRootScannedProjects()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "Root",
            Type = PresentationSourceType.LocalRoot,
            Location = "/tmp/root"
        });
        var scanner = new FakeSourceScanner();
        scanner.Projects.Add(MakeProject("Scanned"));
        var vm = CreateViewModel(settings, scanner);
        await vm.RefreshLibraryAsync();
        vm.SelectedProject = vm.Projects.Single();

        Assert.False(vm.CanRemoveSelectedProject);
        Assert.False(vm.RemoveSelectedProjectFromLibraryCommand.CanExecute(null));
    }

    [Fact]
    public void OpenSelectedProjectLocationCommand_CannotExecute_WhenNothingSelected()
    {
        var vm = CreateViewModel();

        Assert.False(vm.OpenSelectedProjectLocationCommand.CanExecute(null));

        vm.SelectedProject = new PresentationProjectViewModel(MakeProject());
        Assert.True(vm.OpenSelectedProjectLocationCommand.CanExecute(null));
    }
}
