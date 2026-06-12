namespace SlideDevPresenter.Infrastructure.Services;

/// <summary>
/// Abstraction over a running child process. Exists to allow
/// deterministic testing of SlidevProcessHost state transitions.
/// </summary>
internal interface IRunningProcess
{
    bool HasExited { get; }
    void Kill(bool entireProcessTree = true);
    event EventHandler<string?>? OutputDataReceived;
    event EventHandler? Exited;
}
