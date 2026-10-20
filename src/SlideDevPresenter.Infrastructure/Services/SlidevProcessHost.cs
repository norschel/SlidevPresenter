using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class SlidevProcessHost : ISlidevProcessHost
{
    private readonly ILogger<SlidevProcessHost> _logger;
    private readonly Func<ProcessStartInfo, IRunningProcess> _processFactory;
    private readonly ISlideDeckBuilder? _builder;
    private CancellationTokenSource? _startupCts;
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
        : this(logger, DefaultProcessFactory, new SlideDeckBuilder()) { }

    internal SlidevProcessHost(ILogger<SlidevProcessHost> logger, Func<ProcessStartInfo, IRunningProcess> processFactory, ISlideDeckBuilder? builder = null)
    {
        _logger = logger;
        _processFactory = processFactory;
        _builder = builder;
    }

    public async Task StartAsync(PresentationProject project, int port, CancellationToken cancellationToken = default)
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

        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_lock) { _startupCts = startup; }
        var slidesPath = project.SlidesFilePath ?? Path.Combine(project.Location, "slides.md");
        var psi = SlideDeckBuilder.CreateStartInfo("node", project.Location,
            SlideDeckBuilder.GetCliPath(Path.GetFullPath(project.Location)), Path.GetFullPath(slidesPath), "--port", port.ToString());

        IRunningProcess process;
        try
        {
            if (_builder is not null)
                await _builder.BuildAsync(project, startup.Token);
            startup.Token.ThrowIfCancellationRequested();
            process = _processFactory(psi);
        }
        catch (OperationCanceledException) when (startup.IsCancellationRequested)
        {
            bool isCurrent;
            lock (_lock) { isCurrent = ReferenceEquals(_startupCts, startup); }
            if (isCurrent)
                await StopAsync();
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Slidev process for {Project}.", project.Name);
            ErrorMessage = ex.Message;
            TransitionState(HostState.Error);
            return;
        }
        finally
        {
            lock (_lock)
            {
                if (ReferenceEquals(_startupCts, startup))
                    _startupCts = null;
            }
        }

        process.OutputDataReceived += OnOutputDataReceived;
        process.Exited += OnProcessExited;

        lock (_lock) { _process = process; }

    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IRunningProcess? proc;
        lock (_lock)
        {
            _startupCts?.Cancel();
            _startupCts = null;
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
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new RunningProcessAdapter(process);
    }
}
