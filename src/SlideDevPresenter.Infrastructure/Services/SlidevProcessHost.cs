using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class SlidevProcessHost : ISlidevProcessHost
{
    private readonly ILogger<SlidevProcessHost> _logger;
    private readonly Func<ProcessStartInfo, IRunningProcess> _processFactory;
    private IRunningProcess? _process;
    private readonly object _lock = new();
    private HostState _state = HostState.Idle;

    public HostState State
    {
        get { lock (_lock) return _state; }
    }

    public string? ParticipantUrl { get; private set; }
    public string? PresenterUrl { get; private set; }
    public int? Port { get; private set; }
    public string? ErrorMessage { get; private set; }

    public event EventHandler<HostStateChangedEventArgs>? StateChanged;

    public SlidevProcessHost(ILogger<SlidevProcessHost> logger)
        : this(logger, DefaultProcessFactory) { }

    internal SlidevProcessHost(ILogger<SlidevProcessHost> logger, Func<ProcessStartInfo, IRunningProcess> processFactory)
    {
        _logger = logger;
        _processFactory = processFactory;
    }

    public Task StartAsync(PresentationProject project, int port, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_state is HostState.Starting or HostState.Running)
                throw new InvalidOperationException("A Slidev process is already running.");
        }

        Port = port;
        ParticipantUrl = null;
        PresenterUrl = null;
        ErrorMessage = null;
        TransitionState(HostState.Starting);

        var slidesPath = project.SlidesFilePath ?? Path.Combine(project.Location, "slides.md");
        var psi = new ProcessStartInfo
        {
            FileName = "npx",
            Arguments = $"slidev \"{slidesPath}\" --port {port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        IRunningProcess process;
        try
        {
            process = _processFactory(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Slidev process for {Project}.", project.Name);
            ErrorMessage = ex.Message;
            TransitionState(HostState.Error);
            return Task.CompletedTask;
        }

        process.OutputDataReceived += OnOutputDataReceived;
        process.Exited += OnProcessExited;

        lock (_lock) { _process = process; }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IRunningProcess? proc;
        lock (_lock)
        {
            proc = _process;
            _process = null;
        }

        if (proc is not null)
        {
            proc.OutputDataReceived -= OnOutputDataReceived;
            proc.Exited -= OnProcessExited;

            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error killing Slidev process."); }
            }
        }

        Port = null;
        ParticipantUrl = null;
        PresenterUrl = null;
        ErrorMessage = null;
        TransitionState(HostState.Idle);
        return Task.CompletedTask;
    }

    private void OnOutputDataReceived(object? sender, string? data)
    {
        if (data is null) return;

        _logger.LogDebug("[slidev] {Line}", data);

        // Detect the ready line; Slidev prints: "  ➜  Local:   http://localhost:3030/"
        if (State == HostState.Starting && data.Contains("Local:", StringComparison.OrdinalIgnoreCase))
        {
            var url = ExtractUrl(data);
            if (url is not null)
            {
                ParticipantUrl = url;
                PresenterUrl = url.TrimEnd('/') + "/presenter/";
                _logger.LogInformation("Slidev ready at {Url}.", url);
                TransitionState(HostState.Running);
            }
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        bool shouldError;
        lock (_lock)
        {
            shouldError = _state is HostState.Running or HostState.Starting;
        }

        if (shouldError)
        {
            _logger.LogWarning("Slidev process exited unexpectedly.");
            ErrorMessage = "Slidev process exited unexpectedly.";
            TransitionState(HostState.Error);
        }
    }

    private void TransitionState(HostState newState)
    {
        lock (_lock) { _state = newState; }
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(
            newState,
            newState == HostState.Error ? ErrorMessage : null));
    }

    private static string? ExtractUrl(string line)
    {
        var idx = line.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            idx = line.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var url = line[idx..].Trim();
        var end = url.IndexOfAny([' ', '\t']);
        return end >= 0 ? url[..end] : url;
    }

    private static IRunningProcess DefaultProcessFactory(ProcessStartInfo psi)
    {
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();
        process.BeginOutputReadLine();
        return new RunningProcessAdapter(process);
    }
}
