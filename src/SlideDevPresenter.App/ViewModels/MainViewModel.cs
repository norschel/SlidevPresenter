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
    private readonly ISettingsService _settingsService;
    private readonly ISourceScanner _sourceScanner;
    private readonly ISlidevProcessHost _processHost;
    private readonly SynchronizationContext? _syncContext;

    public ObservableCollection<PresentationProjectViewModel> Projects { get; } = [];

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
    private string? _participantUrl;

    [ObservableProperty]
    private string? _presenterUrl;

    [ObservableProperty]
    private int? _port;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isRefreshing;

    public bool IsIdle => HostState == HostState.Idle;
    public bool IsStarting => HostState == HostState.Starting;
    public bool IsRunning => HostState == HostState.Running;
    public bool IsError => HostState == HostState.Error;

    public ISettingsService SettingsService => _settingsService;

    public MainViewModel(ISettingsService settingsService, ISourceScanner sourceScanner, ISlidevProcessHost processHost)
    {
        _settingsService = settingsService;
        _sourceScanner = sourceScanner;
        _processHost = processHost;
        _syncContext = SynchronizationContext.Current;

        _processHost.StateChanged += OnHostStateChanged;
    }

    private void OnHostStateChanged(object? sender, HostStateChangedEventArgs e)
    {
        void Update()
        {
            HostState = e.NewState;
            ParticipantUrl = _processHost.ParticipantUrl;
            PresenterUrl = _processHost.PresenterUrl;
            Port = _processHost.Port;
            ErrorMessage = e.ErrorMessage;
        }

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

            Projects.Clear();
            foreach (var p in projects)
                Projects.Add(p);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    public async Task LaunchAsync()
    {
        if (SelectedProject is null) return;
        var port = _settingsService.Settings.Defaults.DefaultPort;
        await _processHost.StartAsync(SelectedProject.ToModel(), port);
    }

    private bool CanLaunch() => SelectedProject is not null && HostState == HostState.Idle;

    [RelayCommand(CanExecute = nameof(CanStop))]
    public async Task StopAsync()
    {
        await _processHost.StopAsync();
    }

    private bool CanStop() => HostState is HostState.Starting or HostState.Running;

    [RelayCommand(CanExecute = nameof(CanRetry))]
    public async Task RetryAsync()
    {
        if (SelectedProject is null) return;
        await _processHost.StopAsync();
        var port = _settingsService.Settings.Defaults.DefaultPort;
        await _processHost.StartAsync(SelectedProject.ToModel(), port);
    }

    private bool CanRetry() => SelectedProject is not null && HostState == HostState.Error;

    [RelayCommand]
    public void OpenExternalBrowser()
    {
        var url = ParticipantUrl;
        if (string.IsNullOrEmpty(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { ErrorMessage = $"Could not open browser: {ex.Message}"; }
    }
}
