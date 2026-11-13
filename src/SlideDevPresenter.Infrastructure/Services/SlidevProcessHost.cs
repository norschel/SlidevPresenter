using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class SlidevProcessHost : ISlidevProcessHost
{
    private static readonly TimeSpan DefaultReadinessTimeout = TimeSpan.FromSeconds(45);
    private readonly ILogger<SlidevProcessHost> _logger;
    private readonly Func<ProcessStartInfo, IRunningProcess> _processFactory;
    private readonly ISlideDeckBuilder? _builder;
    private readonly TimeSpan _readinessTimeout;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private CancellationTokenSource? _startupCts;
    private Task? _startTask;
    private IRunningProcess? _process;
    private Task? _readinessTask;
    private TaskCompletionSource? _ready;
    private EventHandler<string?>? _processOutputHandler;
    private EventHandler? _processExitHandler;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _terminationLock = new(1, 1);
    private IRunningProcess? _disposedProcess;
    private IRunningProcess? _stoppingProcess;
    private readonly Action? _afterReadyOutputParsed;
    private HostState _state = HostState.Idle;

    public HostState State { get { lock (_lock) return _state; } }
    public string? ParticipantUrl { get; private set; }
    public string? PresenterUrl { get; private set; }
    public int? Port { get; private set; }
    public string? ErrorMessage { get; private set; }
    public event EventHandler<HostStateChangedEventArgs>? StateChanged;

    public SlidevProcessHost(ILogger<SlidevProcessHost> logger)
        : this(logger, DefaultProcessFactory, new SlideDeckBuilder()) { }

    internal SlidevProcessHost(
        ILogger<SlidevProcessHost> logger,
        Func<ProcessStartInfo, IRunningProcess> processFactory,
        ISlideDeckBuilder? builder = null,
        TimeSpan? readinessTimeout = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Action? afterReadyOutputParsed = null)
    {
        _logger = logger;
        _processFactory = processFactory;
        _builder = builder;
        _readinessTimeout = readinessTimeout ?? DefaultReadinessTimeout;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        _afterReadyOutputParsed = afterReadyOutputParsed;
    }

    internal Task? ReadinessTask { get { lock (_lock) return _readinessTask; } }

    public Task StartAsync(PresentationProject project, int port, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource startup;
        TaskCompletionSource ready;
        lock (_lock)
        {
            if (_state is HostState.Starting or HostState.Running || _process is not null || _startupCts is not null)
                throw new InvalidOperationException("A Slidev process is already running.");

            startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _startupCts = startup;
            _ready = ready;
            _state = HostState.Starting;
            Port = port;
            ParticipantUrl = null;
            PresenterUrl = null;
            ErrorMessage = null;
        }
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Starting));
        var startTask = StartCoreAsync(project, port, startup, ready);
        lock (_lock) _startTask = startTask;
        return startTask;
    }

    private async Task StartCoreAsync(
        PresentationProject project,
        int port,
        CancellationTokenSource startup,
        TaskCompletionSource ready)
    {
        IRunningProcess? process = null;
        try
        {
            if (_builder is not null)
                await _builder.BuildAsync(project, startup.Token);
            startup.Token.ThrowIfCancellationRequested();

            var slidesPath = project.SlidesFilePath ?? Path.Combine(project.Location, "slides.md");
            var psi = SlideDeckBuilder.CreateStartInfo("node", project.Location,
                SlideDeckBuilder.GetCliPath(Path.GetFullPath(project.Location)), Path.GetFullPath(slidesPath), "--port", port.ToString());
            process = _processFactory(psi);
            _processOutputHandler = (_, data) => OnOutputDataReceived(process, data);
            _processExitHandler = (_, e) => OnProcessExited(process, e);
            process.OutputDataReceived += _processOutputHandler;
            process.Exited += _processExitHandler;

            lock (_lock)
            {
                if (!ReferenceEquals(_startupCts, startup) || startup.IsCancellationRequested)
                    throw new OperationCanceledException(startup.Token);
                _process = process;
            }

            // Attach and publish ownership before activation; some processes emit output or exit immediately.
            process.Start();
            if (process.HasExited)
                OnProcessExited(process, EventArgs.Empty);

            lock (_lock)
            {
                if (ReferenceEquals(_startupCts, startup) && State == HostState.Starting)
                    _readinessTask = MonitorReadinessAsync(process, ready, startup.Token);
            }
        }
        catch (OperationCanceledException) when (startup.IsCancellationRequested)
        {
            if (process is not null)
            {
                try
                {
                    await TerminateOwnedProcessAsync(process);
                }
                catch (Exception exception)
                {
                    ErrorMessage = exception.Message;
                    TransitionState(HostState.Error);
                    throw;
                }
            }
            lock (_lock)
            {
                if (ReferenceEquals(_startupCts, startup))
                {
                    _startupCts = null;
                    _process = null;
                    _ready = null;
                }
            }
            if (State != HostState.Idle)
                TransitionState(HostState.Idle);
            startup.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to start Slidev process for {Project}.", project.Name);
            var reportedException = exception;
            if (process is not null)
            {
                try
                {
                    await TerminateOwnedProcessAsync(process);
                }
                catch (Exception stopException)
                {
                    _logger.LogError(stopException, "Could not clean up the Slidev process after startup failed.");
                    reportedException = new InvalidOperationException(
                        $"{exception.Message} The process could not be stopped: {stopException.Message}",
                        new AggregateException(exception, stopException));
                }
            }

            lock (_lock)
            {
                if (ReferenceEquals(_startupCts, startup) && _process is null)
                {
                    _startupCts = null;
                    _ready = null;
                    startup.Dispose();
                }
            }
            ErrorMessage = reportedException.Message;
            TransitionState(HostState.Error);
        }
    }

    private async Task MonitorReadinessAsync(IRunningProcess process, TaskCompletionSource ready, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeout = _delay(_readinessTimeout, timeoutCts.Token);
            var completed = await Task.WhenAny(ready.Task, timeout);
            if (completed == ready.Task)
            {
                timeoutCts.Cancel();
                return;
            }

            await timeout;
            const string message = "Slidev did not report a ready URL before the startup deadline. Check the project and port, then retry.";
            if (!TryClaimReadinessTimeout(process, cancellationToken, message))
                return;

            _logger.LogError("Slidev readiness timed out after {Timeout}.", _readinessTimeout);
            StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Error, message));
            try
            {
                await TerminateOwnedProcessAsync(process);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Slidev remained active after readiness timed out.");
                ErrorMessage = $"{message} The process could not be stopped: {exception.Message}";
                TransitionState(HostState.Error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private bool TryClaimReadinessTimeout(IRunningProcess process, CancellationToken cancellationToken, string message)
    {
        lock (_lock)
        {
            if (cancellationToken.IsCancellationRequested
                || !ReferenceEquals(_process, process)
                || _state != HostState.Starting)
                return false;

            ErrorMessage = message;
            _state = HostState.Error;
        }

        return true;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        IRunningProcess? process;
        CancellationTokenSource? startup;
        Task? startTask;
        lock (_lock)
        {
            startup = _startupCts;
            startup?.Cancel();
            startTask = _startTask;
        }

        if (startTask is not null)
            await startTask;

        lock (_lock) process = _process;
        if (process is not null)
            await TerminateOwnedProcessAsync(process);

        lock (_lock)
        {
            if (ReferenceEquals(_process, process))
                _process = null;
            if (ReferenceEquals(_startupCts, startup))
                _startupCts = null;
            _startTask = null;
            _ready?.TrySetCanceled();
            _ready = null;
            _readinessTask = null;
        }

        Port = null;
        ParticipantUrl = null;
        PresenterUrl = null;
        ErrorMessage = null;
        TransitionState(HostState.Idle);
        startup?.Dispose();
    }

    private async Task TerminateOwnedProcessAsync(IRunningProcess process)
    {
        await _terminationLock.WaitAsync();
        try
        {
            if (ReferenceEquals(_disposedProcess, process))
                return;
            lock (_lock) _stoppingProcess = process;

            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception exception) when (process.HasExited)
                {
                    _logger.LogDebug(exception, "Slidev exited while being stopped.");
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Could not terminate the owned Slidev process.");
                    throw new InvalidOperationException("Slidev is still running because the process could not be stopped.", exception);
                }
            }

            if (!process.HasExited)
                await process.WaitForExitAsync(CancellationToken.None);
            if (_processOutputHandler is not null)
                process.OutputDataReceived -= _processOutputHandler;
            if (_processExitHandler is not null)
                process.Exited -= _processExitHandler;
            process.Dispose();
            lock (_lock)
            {
                _disposedProcess = process;
                _processOutputHandler = null;
                _processExitHandler = null;
                if (ReferenceEquals(_stoppingProcess, process))
                    _stoppingProcess = null;
                if (ReferenceEquals(_process, process))
                    _process = null;
            }
        }
        catch
        {
            lock (_lock)
            {
                if (ReferenceEquals(_stoppingProcess, process))
                    _stoppingProcess = null;
            }
            throw;
        }
        finally
        {
            _terminationLock.Release();
        }
    }

    private void OnOutputDataReceived(IRunningProcess process, string? data)
    {
        if (data is null) return;
        _logger.LogDebug("[slidev] {Line}", data);
        if (!SlidevOutputParser.TryParseReadyUrl(data, out var url))
            return;

        _afterReadyOutputParsed?.Invoke();
        var participantUrl = url.AbsoluteUri;
        lock (_lock)
        {
            if (!ReferenceEquals(_process, process) || _state != HostState.Starting)
                return;

            Port = url.Port;
            ParticipantUrl = participantUrl;
            PresenterUrl = participantUrl.TrimEnd('/') + "/presenter/";
            _ready?.TrySetResult();
            _state = HostState.Running;
        }

        _logger.LogInformation("Slidev ready at {Url}.", participantUrl);
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Running));
    }

    private void OnProcessExited(IRunningProcess process, EventArgs e)
    {
        bool raiseError;
        lock (_lock)
        {
            if (!ReferenceEquals(_process, process))
                return;

            _ready?.TrySetCanceled();
            if (ReferenceEquals(_stoppingProcess, process))
                return;

            raiseError = _state is HostState.Running or HostState.Starting;
            if (raiseError)
            {
                ErrorMessage = "Slidev process exited unexpectedly.";
                _state = HostState.Error;
            }
        }

        if (raiseError)
        {
            _logger.LogWarning("Slidev process exited unexpectedly.");
            StateChanged?.Invoke(this, new HostStateChangedEventArgs(HostState.Error, ErrorMessage));
        }
    }

    private void TransitionState(HostState newState)
    {
        lock (_lock) _state = newState;
        StateChanged?.Invoke(this, new HostStateChangedEventArgs(
            newState,
            newState == HostState.Error ? ErrorMessage : null));
    }

    private static IRunningProcess DefaultProcessFactory(ProcessStartInfo psi) =>
        new RunningProcessAdapter(new Process { StartInfo = psi, EnableRaisingEvents = true });
}
