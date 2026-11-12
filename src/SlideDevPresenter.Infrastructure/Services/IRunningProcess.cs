namespace SlideDevPresenter.Infrastructure.Services;

/// <summary>
/// Abstraction over a running child process. Exists to allow
/// deterministic testing of SlidevProcessHost state transitions.
/// </summary>
internal interface IRunningProcess : IDisposable
{
    bool HasExited { get; }
    void Start();
    void Kill(bool entireProcessTree = true);
    Task WaitForExitAsync(CancellationToken cancellationToken);
    event EventHandler<string?>? OutputDataReceived;
    event EventHandler? Exited;
}
